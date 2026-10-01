using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Avatar Object Database — browse/filter every scanned modular avatar part, edit its AOD metadata
/// (allowed genders/roles, color variants, selection weight), and flag it reviewed. Opens via
/// Ctrl+Shift+A (see TopBarUI) rather than a 0-9 key — every slot 0-9 is already spoken for by real
/// gameplay panels (see UIKeyBindingManager's own key list), and this is a dev-workflow tool, not a
/// player-facing one, so it doesn't compete for one.
///
/// Built the same way as ShiftManagerPanel/ContractsPanel/PurchasingPanel: plain C# class
/// implementing IUIPanel, programmatic UI Toolkit (no UXML) added directly into the shared
/// TopBarUI document root, navy/blue/orange palette matching every other full-screen panel.
///
/// Data lives entirely in AvatarPartLibrary/AvatarWeightConfig (see ModularAvatarImporter/
/// ModularAvatarAssembler) — this panel is purely a view+editor over that ScriptableObject data.
/// Edits persist via AssetDatabase (Editor-only — this tool has no meaning in a built player, same
/// as ModularAvatarImporter itself).
///
/// Live 3D preview (AODPreviewStage) wired in 2026-09-26; three-column redesign (grid card
/// thumbnails, dedicated rotatable preview column, palette-driven color variants) 2026-09-27 per
/// Tad's annotated mockup.
/// </summary>
public class AODPanel : IUIPanel
{
    // ── Palette — matches every other full-screen panel in the game ──
    private static readonly Color ColBg          = new Color(18f / 255f, 26f / 255f, 36f / 255f, 1f);
    private static readonly Color ColPanelLight  = new Color(28f / 255f, 38f / 255f, 50f / 255f, 1f);
    private static readonly Color ColBorder      = new Color(0x5C / 255f, 0x9B / 255f, 0xC4 / 255f, 1f);
    private static readonly Color ColTitleText   = new Color(0xCF / 255f, 0xE2 / 255f, 0xF0 / 255f, 1f);
    private static readonly Color ColSubtleText  = new Color(0x7A / 255f, 0x99 / 255f, 0xB0 / 255f, 1f);
    private static readonly Color ColOrange      = new Color(0xB5 / 255f, 0x74 / 255f, 0x3A / 255f, 1f);
    private static readonly Color ColOrangeEdge  = new Color(0x7A / 255f, 0x4C / 255f, 0x22 / 255f, 1f);
    private static readonly Color ColOrangeText  = new Color(0xFD / 255f, 0xE8 / 255f, 0xCC / 255f, 1f);
    private static readonly Color ColOrangeHover = new Color(0xC6 / 255f, 0x7F / 255f, 0x42 / 255f, 1f);
    private static readonly Color ColBlue        = new Color(0x4C / 255f, 0x90 / 255f, 0xC0 / 255f, 1f);
    private static readonly Color ColBlueEdge    = new Color(0x2C / 255f, 0x5E / 255f, 0x82 / 255f, 1f);
    private static readonly Color ColBlueHover    = new Color(0x5B / 255f, 0xA6 / 255f, 0xD8 / 255f, 1f);
    private static readonly Color ColCellEven    = new Color(36f / 255f, 48f / 255f, 62f / 255f, 0.75f);
    private static readonly Color ColCellOdd     = new Color(30f / 255f, 40f / 255f, 52f / 255f, 0.75f);
    private static readonly Color ColFireRed     = new Color(0xC1 / 255f, 0x27 / 255f, 0x2D / 255f, 1f);
    private static readonly Color ColFireRedEdge = new Color(0x7A / 255f, 0x16 / 255f, 0x1A / 255f, 1f);
    private static readonly Color ColFireRedHover = new Color(0xD8 / 255f, 0x3A / 255f, 0x40 / 255f, 1f);
    private static readonly Color ColGreen       = new Color(0x3F / 255f, 0x8F / 255f, 0x5A / 255f, 1f);
    private static readonly Color ColGreenEdge   = new Color(0x27 / 255f, 0x5C / 255f, 0x3A / 255f, 1f);
    private static readonly Color ColVanilla     = new Color(0xF5 / 255f, 0xF0 / 255f, 0xE1 / 255f, 1f);

    private static Font _lilita;
    private static Font LilitaFont()
    {
        if (_lilita != null) return _lilita;
#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("LilitaOne-Regular t:Font");
        if (guids.Length > 0)
            _lilita = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#else
        _lilita = Resources.Load<Font>("LilitaOne-Regular");
#endif
        return _lilita;
    }

    private static void ApplyFont(VisualElement el, bool bold = false, int size = -1)
    {
        var f = LilitaFont();
        if (f != null) el.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(f));
        if (bold) el.style.unityFontStyleAndWeight = FontStyle.Bold;
        if (size > 0) el.style.fontSize = size;
    }

    /// <summary>Hover-grows + click-shrinks any element — the "living, interactive" feel Tad asked
    /// for, applied uniformly to every clickable thing in this panel (chips, cards, buttons) rather
    /// than one-off per element.</summary>
    private static void AddPressFeedback(VisualElement el, float hoverScale = 1.05f, float pressScale = 0.95f)
    {
        el.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("scale") };
        el.style.transitionDuration = new List<TimeValue> { new TimeValue(120, TimeUnit.Millisecond) };
        el.style.transitionTimingFunction = new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) };
        el.style.scale = new StyleScale(new Scale(Vector3.one));
        el.RegisterCallback<PointerEnterEvent>(_ => el.style.scale = new StyleScale(new Scale(new Vector3(hoverScale, hoverScale, 1f))));
        el.RegisterCallback<PointerLeaveEvent>(_ => el.style.scale = new StyleScale(new Scale(Vector3.one)));
        el.RegisterCallback<PointerDownEvent>(_ => el.style.scale = new StyleScale(new Scale(new Vector3(pressScale, pressScale, 1f))));
        el.RegisterCallback<PointerUpEvent>(_ => el.style.scale = new StyleScale(new Scale(new Vector3(hoverScale, hoverScale, 1f))));
    }

    private static Button MakeButton(string text, Color fill, Color edge, Color hover, Color textCol, int fontSize = 14)
    {
        var btn = new Button { text = text };
        btn.style.backgroundColor = fill;
        btn.style.borderTopWidth = btn.style.borderBottomWidth = btn.style.borderLeftWidth = btn.style.borderRightWidth = 2f;
        btn.style.borderTopColor = btn.style.borderBottomColor = btn.style.borderLeftColor = btn.style.borderRightColor = edge;
        btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius = btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 6f;
        btn.style.color = textCol;
        btn.style.unityTextAlign = TextAnchor.MiddleCenter;
        ApplyFont(btn, true, fontSize);
        AddPressFeedback(btn);
        btn.RegisterCallback<PointerEnterEvent>(_ => btn.style.backgroundColor = hover);
        btn.RegisterCallback<PointerLeaveEvent>(_ => btn.style.backgroundColor = fill);
        return btn;
    }

    /// <summary>A toggle "chip" used throughout the filter bar and role/color editors — click to
    /// flip selected state, visually distinct fill when selected.</summary>
    private class Chip
    {
        public readonly VisualElement Root;
        public bool Selected;
        public event Action<bool> OnChanged;

        public Chip(string label)
        {
            Root = new VisualElement { name = "chip-" + label };
            Root.style.flexDirection = FlexDirection.Row;
            Root.style.paddingLeft = Root.style.paddingRight = 10f;
            Root.style.paddingTop = Root.style.paddingBottom = 5f;
            Root.style.marginRight = 6f;
            Root.style.marginBottom = 6f;
            Root.style.borderTopLeftRadius = Root.style.borderTopRightRadius =
                Root.style.borderBottomLeftRadius = Root.style.borderBottomRightRadius = 14f;
            Root.style.borderTopWidth = Root.style.borderBottomWidth = Root.style.borderLeftWidth = Root.style.borderRightWidth = 2f;
            Root.pickingMode = PickingMode.Position;

            var label_ = new Label(label);
            ApplyFont(label_, true, 12);
            label_.pickingMode = PickingMode.Ignore;
            Root.Add(label_);

            AddPressFeedback(Root, 1.08f, 0.94f);
            Root.RegisterCallback<PointerUpEvent>(e => { if (e.button == 0) Toggle(); });
            ApplyVisual();
        }

        public void Toggle() { Selected = !Selected; ApplyVisual(); OnChanged?.Invoke(Selected); }

        public void SetSelected(bool value, bool silent = false)
        {
            if (Selected == value) return;
            Selected = value;
            ApplyVisual();
            if (!silent) OnChanged?.Invoke(Selected);
        }

        private void ApplyVisual()
        {
            Root.style.backgroundColor = Selected ? ColOrange : ColPanelLight;
            Root.style.borderTopColor = Root.style.borderBottomColor = Root.style.borderLeftColor = Root.style.borderRightColor
                = Selected ? ColOrangeEdge : ColBlueEdge;
            ((Label)Root[0]).style.color = Selected ? ColOrangeText : ColSubtleText;
        }
    }

    // ── State ──
    private readonly VisualElement _overlay;
    private readonly VisualElement _modal;
    private readonly VisualElement _titleBar;
    private readonly VisualElement _body;
    private readonly VisualElement _gridScroll;
    private readonly VisualElement _grid;
    private readonly VisualElement _previewColumn;
    private readonly VisualElement _previewFrame;
    private readonly Image _previewImage;
    private readonly Label _previewEmptyHint;
    private readonly Label _previewTitleLabel;
    private readonly VisualElement _detailPanel;
    private readonly VisualElement _employeeCategoryBar;
    private readonly Label _countLabel;
    private readonly Button _minimizeBtn;
    private readonly Button _maximizeBtn;
    private readonly DraggableWindow _drag;
    private readonly ResizableWindow _resize;

    private readonly List<Chip> _genderChips = new();
    private readonly List<(EmployeeRole role, Chip chip)> _roleChips = new();
    private readonly Dictionary<string, Chip> _slotChips = new();
    private Chip _missingOnlyChip;

    private bool _visible;
    private bool _minimized;
    private IAvatarPart _selectedPart;
    private Label _submitErrorLabel;

    // ── "Pimp My Employee" mode — editing one specific live employee's cosmetic slots instead of
    // browsing/tagging the global part library. Null = normal library-browse mode. ──
    private EmployeeIdentity _employeeIdentity;
    private EmployeeSpawner _employeeSpawner;
    private readonly Dictionary<string, string> _pendingOverrides = new();
    private string _employeeCategory;

    private static readonly (string key, string label)[] EmployeeCategories =
    {
        ("hair", "Hairstyle"),
        ("hat.hardhat", "Hard Hat"),
        ("hat.headphones", "Headphones"),
        ("facialhair", "Facial Hair"),
    };

    public bool IsOpen => _visible;

    public AODPanel(VisualElement root)
    {
        _overlay = new VisualElement { name = "aod-overlay" };
        _overlay.style.position = Position.Absolute;
        _overlay.style.left = 0; _overlay.style.top = 0; _overlay.style.right = 0; _overlay.style.bottom = 0;
        _overlay.style.backgroundColor = new Color(0, 0, 0, 0.85f);
        _overlay.style.display = DisplayStyle.None;
        _overlay.pickingMode = PickingMode.Position;

        _modal = new VisualElement { name = "aod-modal" };
        _modal.style.position = Position.Absolute;
        _modal.style.left = 60; _modal.style.top = 60; _modal.style.width = 1100; _modal.style.height = 700;
        _modal.style.backgroundColor = ColBg;
        _modal.style.borderTopWidth = _modal.style.borderBottomWidth = _modal.style.borderLeftWidth = _modal.style.borderRightWidth = 3f;
        _modal.style.borderTopColor = _modal.style.borderBottomColor = _modal.style.borderLeftColor = _modal.style.borderRightColor = ColBorder;
        _modal.style.borderTopLeftRadius = _modal.style.borderTopRightRadius =
            _modal.style.borderBottomLeftRadius = _modal.style.borderBottomRightRadius = 8f;
        _overlay.Add(_modal);

        // ── Title bar ──
        _titleBar = new VisualElement { name = "aod-titlebar" };
        _titleBar.style.flexDirection = FlexDirection.Row;
        _titleBar.style.height = 52f;
        _titleBar.style.flexShrink = 0f; // a fixed-height row is only a suggestion until flexShrink:0 backs it — bit us before (ContractsPanel/PurchasingPanel), guarding it here from the start
        _titleBar.style.paddingLeft = _titleBar.style.paddingRight = 14f;
        _titleBar.style.alignItems = Align.Center;
        _titleBar.style.backgroundColor = ColPanelLight;
        _titleBar.style.borderBottomWidth = 2f;
        _titleBar.style.borderBottomColor = ColBorder;
        _modal.Add(_titleBar);

        var title = new Label("AVATAR OBJECT DATABASE");
        ApplyFont(title, true, 20);
        title.style.color = ColTitleText;
        title.style.flexGrow = 1f;
        _titleBar.Add(title);

        _countLabel = new Label();
        ApplyFont(_countLabel, false, 13);
        _countLabel.style.color = ColSubtleText;
        _countLabel.style.marginRight = 16f;
        _titleBar.Add(_countLabel);

        _minimizeBtn = MakeButton("_", ColBlue, ColBlueEdge, ColBlueHover, ColVanilla, 18);
        _minimizeBtn.style.width = 36; _minimizeBtn.style.height = 32; _minimizeBtn.style.marginRight = 6f;
        _minimizeBtn.clicked += ToggleMinimize;
        _titleBar.Add(_minimizeBtn);

        _maximizeBtn = MakeButton("□", ColBlue, ColBlueEdge, ColBlueHover, ColVanilla, 16);
        _maximizeBtn.style.width = 36; _maximizeBtn.style.height = 32; _maximizeBtn.style.marginRight = 6f;
        _maximizeBtn.clicked += () => _resize.CycleScale();
        _titleBar.Add(_maximizeBtn);

        var closeBtn = MakeButton("X", ColFireRed, ColFireRedEdge, ColFireRedHover, ColVanilla, 16);
        closeBtn.style.width = 36; closeBtn.style.height = 32;
        closeBtn.clicked += Hide;
        _titleBar.Add(closeBtn);

        _drag = new DraggableWindow(_modal, _titleBar, closeBtn);
        _resize = new ResizableWindow(_modal, 700f, 420f);

        // ── Body (everything below the title bar — hidden while minimized) ──
        _body = new VisualElement { name = "aod-body" };
        _body.style.flexGrow = 1f;
        _body.style.flexDirection = FlexDirection.Column;
        _body.style.overflow = Overflow.Hidden;
        _modal.Add(_body);

        _body.Add(BuildFilterBar());

        // ── Content split: grid (left) | preview (center) | metadata editor (right) ──
        var content = new VisualElement { name = "aod-content" };
        content.style.flexDirection = FlexDirection.Row;
        content.style.flexGrow = 1f;
        content.style.overflow = Overflow.Hidden;
        _body.Add(content);

        var gridColumn = new VisualElement { name = "aod-grid-column" };
        gridColumn.style.flexDirection = FlexDirection.Column;
        gridColumn.style.flexGrow = 1f;
        gridColumn.style.overflow = Overflow.Hidden;
        content.Add(gridColumn);

        // Category tabs for "Pimp My Employee" mode (Hairstyle/Hard Hat/Headphones/Facial Hair) —
        // hidden in normal library-browse mode, shown above the grid when editing a specific
        // employee (see ShowForEmployee/RefreshEmployeeMode).
        _employeeCategoryBar = new VisualElement { name = "aod-employee-categories" };
        _employeeCategoryBar.style.flexDirection = FlexDirection.Row;
        _employeeCategoryBar.style.flexWrap = Wrap.Wrap;
        _employeeCategoryBar.style.flexShrink = 0f;
        _employeeCategoryBar.style.paddingLeft = _employeeCategoryBar.style.paddingTop = 12f;
        _employeeCategoryBar.style.display = DisplayStyle.None;
        gridColumn.Add(_employeeCategoryBar);

        _gridScroll = new ScrollView(ScrollViewMode.Vertical) { name = "aod-grid-scroll" };
        _gridScroll.style.flexGrow = 1f;
        _gridScroll.style.paddingLeft = _gridScroll.style.paddingTop = _gridScroll.style.paddingRight = 12f;
        gridColumn.Add(_gridScroll);

        _grid = new VisualElement { name = "aod-grid" };
        _grid.style.flexDirection = FlexDirection.Row;
        _grid.style.flexWrap = Wrap.Wrap;
        ((ScrollView)_gridScroll).Add(_grid);

        // ── Center preview column ──
        _previewColumn = new VisualElement { name = "aod-preview-column" };
        _previewColumn.style.width = 420f;
        _previewColumn.style.flexShrink = 0f;
        _previewColumn.style.flexDirection = FlexDirection.Column;
        _previewColumn.style.alignItems = Align.Center;
        _previewColumn.style.paddingLeft = _previewColumn.style.paddingRight = 20f;
        _previewColumn.style.paddingTop = 16f;
        _previewColumn.style.borderLeftWidth = 2f; _previewColumn.style.borderLeftColor = ColBorder;
        _previewColumn.style.borderRightWidth = 2f; _previewColumn.style.borderRightColor = ColBorder;
        content.Add(_previewColumn);

        _previewFrame = new VisualElement { name = "aod-preview-frame" };
        _previewFrame.style.width = 380f;
        _previewFrame.style.height = 380f;
        _previewFrame.style.flexShrink = 0f;
        _previewFrame.style.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 1f);
        _previewFrame.style.borderTopLeftRadius = _previewFrame.style.borderTopRightRadius =
            _previewFrame.style.borderBottomLeftRadius = _previewFrame.style.borderBottomRightRadius = 8f;
        _previewFrame.style.borderTopWidth = _previewFrame.style.borderBottomWidth =
            _previewFrame.style.borderLeftWidth = _previewFrame.style.borderRightWidth = 2f;
        _previewFrame.style.borderTopColor = _previewFrame.style.borderBottomColor =
            _previewFrame.style.borderLeftColor = _previewFrame.style.borderRightColor = ColBorder;
        _previewFrame.style.overflow = Overflow.Hidden;
        _previewFrame.style.justifyContent = Justify.Center;
        _previewFrame.style.alignItems = Align.Center;
        _previewFrame.style.cursor = new StyleCursor(new UnityEngine.UIElements.Cursor()); // default arrow; drag rotate has no dedicated cursor asset
        _previewColumn.Add(_previewFrame);

        _previewImage = new Image();
        _previewImage.style.width = Length.Percent(100);
        _previewImage.style.height = Length.Percent(100);
        _previewImage.scaleMode = ScaleMode.ScaleToFit;
        _previewImage.image = AODPreviewStage.Texture;
        _previewImage.style.display = DisplayStyle.None;
        _previewFrame.Add(_previewImage);

        _previewEmptyHint = new Label("Select an item\nto preview & rotate");
        ApplyFont(_previewEmptyHint, false, 13);
        _previewEmptyHint.style.color = ColSubtleText;
        _previewEmptyHint.style.unityTextAlign = TextAnchor.MiddleCenter;
        _previewEmptyHint.style.whiteSpace = WhiteSpace.Normal;
        _previewFrame.Add(_previewEmptyHint);

        WireDragRotate(_previewImage);

        _previewTitleLabel = new Label();
        ApplyFont(_previewTitleLabel, true, 20);
        _previewTitleLabel.style.color = ColTitleText;
        _previewTitleLabel.style.marginTop = 14f;
        _previewTitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _previewTitleLabel.style.whiteSpace = WhiteSpace.Normal;
        _previewColumn.Add(_previewTitleLabel);

        // ── Right metadata editor — one big scroll region per Tad's spec ──
        _detailPanel = new VisualElement { name = "aod-detail" };
        _detailPanel.style.width = 300f;
        _detailPanel.style.flexShrink = 0f;
        _detailPanel.style.backgroundColor = ColPanelLight;
        content.Add(_detailPanel);
        ShowEmptyDetail();

        root.Add(_overlay);
        Hide();
    }

    /// <summary>Left-click drag rotates the currently-previewed part: horizontal drag yaws (Y axis),
    /// vertical drag pitches (X axis) — per Tad's spec.</summary>
    private void WireDragRotate(VisualElement target)
    {
        bool dragging = false;
        int pid = -1;
        Vector2 lastPos = Vector2.zero;

        target.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0 || _selectedPart == null) return;
            dragging = true;
            pid = e.pointerId;
            lastPos = e.position;
            target.CapturePointer(pid);
            e.StopPropagation();
        });
        target.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (!dragging || e.pointerId != pid) return;
            Vector2 delta = (Vector2)e.position - lastPos;
            lastPos = e.position;
            AODPreviewStage.Rotate(delta.x * 0.5f, -delta.y * 0.5f);
        });
        target.RegisterCallback<PointerUpEvent>(e =>
        {
            if (e.pointerId != pid) return;
            dragging = false;
            if (target.HasPointerCapture(pid)) target.ReleasePointer(pid);
        });
    }

    // ── Filter bar ──
    private VisualElement BuildFilterBar()
    {
        var bar = new VisualElement { name = "aod-filterbar" };
        bar.style.flexShrink = 0f;
        bar.style.paddingLeft = bar.style.paddingRight = bar.style.paddingTop = 10f;
        bar.style.paddingBottom = 4f;
        bar.style.borderBottomWidth = 1f;
        bar.style.borderBottomColor = ColBorder;

        VisualElement Row(string label)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4f;
            var lbl = new Label(label);
            ApplyFont(lbl, true, 12);
            lbl.style.color = ColSubtleText;
            lbl.style.width = 60f;
            lbl.style.flexShrink = 0f;
            row.Add(lbl);
            bar.Add(row);
            return row;
        }

        var genderRow = Row("GENDER");
        foreach (var g in new[] { "male", "female", "neutral" })
        {
            var chip = new Chip(g.Substring(0, 1).ToUpper() + g.Substring(1));
            chip.OnChanged += _ => Refresh();
            _genderChips.Add(chip);
            genderRow.Add(chip.Root);
        }

        var roleRow = Row("ROLE");
        foreach (EmployeeRole role in Enum.GetValues(typeof(EmployeeRole)))
        {
            var chip = new Chip(role.DisplayName());
            chip.OnChanged += _ => Refresh();
            _roleChips.Add((role, chip));
            roleRow.Add(chip.Root);
        }

        var slotRow = Row("SLOT");
        slotRow.name = "aod-slot-row"; // repopulated by RefreshSlotChips once real slots are known
        bar.Add(slotRow);

        var extraRow = Row("SHOW");
        _missingOnlyChip = new Chip("Missing Data Only");
        _missingOnlyChip.OnChanged += _ => Refresh();
        extraRow.Add(_missingOnlyChip.Root);

        var rescanBtn = MakeButton("Rescan Folder", ColGreen, ColGreenEdge, ColGreen, ColVanilla, 12);
        rescanBtn.style.marginLeft = 12f;
#if UNITY_EDITOR
        // ModularAvatarImporter lives in an Editor-only assembly (Assets/.../Actors/Editor/) that
        // this runtime UI folder's assembly has no reference to — reflection sidesteps needing an
        // asmdef change just for this one button. Cheap enough to resolve on every click (a rescan
        // itself is already the expensive part) rather than caching the MethodInfo.
        rescanBtn.clicked += () =>
        {
            var importerType = System.AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return System.Type.EmptyTypes; } })
                .FirstOrDefault(t => t.Name == "ModularAvatarImporter");
            var method = importerType?.GetMethod("ScanAndRebuild",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method != null) method.Invoke(null, new object[] { true });
            else Debug.LogWarning("[AODPanel] Could not find ModularAvatarImporter.ScanAndRebuild via reflection.");
            // A rescan can change what a part actually looks like (re-exported FBX under the same
            // name) — drop cached thumbnails so the grid re-bakes instead of showing stale renders.
            AODPreviewStage.ClearThumbnailCache();
            Refresh();
        };
#else
        rescanBtn.SetEnabled(false);
#endif
        extraRow.Add(rescanBtn);

        return bar;
    }

    private void RefreshSlotChips(IEnumerable<string> slots)
    {
        var slotRow = _overlay.Q<VisualElement>("aod-slot-row");
        if (slotRow == null) return;

        var wanted = new HashSet<string>(slots);
        // Remove chips for slots that no longer exist (a rescan can drop a slot entirely).
        foreach (var key in _slotChips.Keys.Where(k => !wanted.Contains(k)).ToList())
        {
            slotRow.Remove(_slotChips[key].Root);
            _slotChips.Remove(key);
        }
        foreach (var slot in wanted.OrderBy(s => s))
        {
            if (_slotChips.ContainsKey(slot)) continue;
            var chip = new Chip(slot);
            chip.OnChanged += _ => Refresh();
            _slotChips[slot] = chip;
            slotRow.Add(chip.Root);
        }
    }

    // ── Show/Hide ──
    public void Show()
    {
        _visible = true;
        _overlay.style.display = DisplayStyle.Flex;
        if (_minimized) ToggleMinimize();
        // Opens maximized every time per Tad's spec ("take up the whole play area screen") — the
        // mirror-image of ResetToNormal's "always open small" default other panels use. Deferred one
        // frame, same reason PurchasingPanel's own FillScreen call is: on the very first Show() of a
        // session this panel hasn't been through a layout pass yet, so the fill-screen size math has
        // nothing real to measure and would compute 0x0.
        _overlay.schedule.Execute(() =>
        {
            _resize.FillScreenExact();
            // FillScreenExact reserves a strip at the top of the screen for the game's TopBar — Tad
            // explicitly wants this panel to cover it entirely ("I don't want to see that when I'm in
            // this UI"), so stretch the modal upward to reclaim that strip while keeping the same
            // bottom edge FillScreenExact already centered. Read the literal inline values it just
            // wrote (not resolvedStyle) so this doesn't depend on a layout pass having run yet.
            float top = _modal.style.top.value.value;
            float height = _modal.style.height.value.value;
            _modal.style.top = 0f;
            _modal.style.height = top + height;
        }).ExecuteLater(16);

        _employeeCategoryBar.style.display = _employeeIdentity != null ? DisplayStyle.Flex : DisplayStyle.None;
        if (_employeeIdentity != null) RefreshEmployeeMode();
        else Refresh();
    }

    public void Hide()
    {
        _visible = false;
        _overlay.style.display = DisplayStyle.None;
        AODPreviewStage.Clear();
        _employeeIdentity = null;
    }

    /// <summary>Matches ItemCreatorPanel's own Toggle() — this panel also has no 0-9 hotkey (every
    /// slot taken), so TopBarUI opens it from a plain button the same way. Always opens back into
    /// plain library-browse mode, even if the panel was last left mid "Pimp My Employee" edit.</summary>
    public void Toggle() { if (_visible) Hide(); else { _employeeIdentity = null; Show(); } }

    /// <summary>Opens the AOD in "Pimp My Employee" mode — editing THIS specific employee's
    /// cosmetic slots (hair/hardhat/headphones/facial hair) rather than browsing/tagging the global
    /// part library. Called from EmployeeInfoUI's Actions dropdown. Forces the panel open/re-open
    /// even if it's already showing something else.</summary>
    public void ShowForEmployee(EmployeeIdentity identity)
    {
        if (identity?.Record == null) return;
        _employeeIdentity = identity;
        if (_employeeSpawner == null) _employeeSpawner = UnityEngine.Object.FindFirstObjectByType<EmployeeSpawner>();
        _selectedPart = null;
        _pendingOverrides.Clear();
        _employeeCategory = EmployeeCategories[0].key;
        Show();
    }

    private void ToggleMinimize()
    {
        _minimized = !_minimized;
        _body.style.display = _minimized ? DisplayStyle.None : DisplayStyle.Flex;
        if (_minimized)
        {
            _modal.style.height = _titleBar.style.height.value.value;
            _resize.SetInteractable(false);
        }
        else
        {
            _modal.style.height = StyleKeyword.Null;
            _resize.SetInteractable(true);
        }
    }

    // ── Data / filtering ──
    private void Refresh()
    {
        var lib = ModularAvatarAssembler.LoadLibrary();
        _grid.Clear();

        if (lib == null || lib.PartCount == 0)
        {
            _countLabel.text = "0 items";
            var empty = new Label("No parts scanned yet — drop a modular FBX into the drop folder, or click Rescan Folder above.");
            ApplyFont(empty, false, 13);
            empty.style.color = ColSubtleText;
            empty.style.whiteSpace = WhiteSpace.Normal;
            _grid.Add(empty);
            return;
        }

        RefreshSlotChips(lib.AllParts.Select(p => p.Slot).Distinct());

        var selectedGenders = _genderChips.Where(c => c.Selected).Select(c => ((Label)c.Root[0]).text.ToLower()).ToHashSet();
        var selectedRoles = _roleChips.Where(t => t.chip.Selected).Select(t => t.role).ToHashSet();
        var selectedSlots = _slotChips.Where(kv => kv.Value.Selected).Select(kv => kv.Key).ToHashSet();
        bool missingOnly = _missingOnlyChip.Selected;

        var filtered = lib.AllParts.Where(p =>
            (selectedGenders.Count == 0 || selectedGenders.Contains(p.Gender)) &&
            (selectedRoles.Count == 0 || p.AllowedRoles.Count == 0 || p.AllowedRoles.Any(selectedRoles.Contains)) &&
            (selectedSlots.Count == 0 || selectedSlots.Contains(p.Slot)) &&
            (!missingOnly || !p.MetadataReviewed)
        ).OrderBy(p => p.Slot).ThenBy(p => p.Gender).ThenBy(p => p.Variant).ToList();

        _countLabel.text = $"{filtered.Count} of {lib.PartCount} items";

        foreach (var part in filtered)
            _grid.Add(BuildCard(lib, part));
    }

    /// <summary>Small card: a colored bar (by slot, so the grid reads by category at a glance) with
    /// a baked 3D thumbnail of the actual part centered on it, and its name overlaid at the bar's
    /// bottom edge — matches Tad's annotated mockup. Deliberately compact (fixed small size, no
    /// flex-grow) so the grid can show as many of a large library as possible at once.</summary>
    private VisualElement BuildCard(AvatarPartLibrary lib, IAvatarPart part)
    {
        var card = new VisualElement { name = "aod-card" };
        card.style.width = 108f;
        card.style.marginRight = 8f;
        card.style.marginBottom = 8f;
        card.style.backgroundColor = ColCellEven;
        card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = 2f;
        var borderCol = _selectedPart == part ? ColOrange : ColBlueEdge;
        card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = borderCol;
        card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 6f;
        card.style.overflow = Overflow.Hidden;
        AddPressFeedback(card, 1.06f, 0.97f);

        var bar = new VisualElement { name = "aod-card-bar" };
        bar.style.height = 82f;
        bar.style.backgroundColor = ColorForSlot(part.Slot);
        bar.style.justifyContent = Justify.Center;
        bar.style.alignItems = Align.Center;
        bar.pickingMode = PickingMode.Ignore;
        card.Add(bar);

        var thumb = AODPreviewStage.GetOrCaptureThumbnail(lib, part);
        if (thumb != null)
        {
            var thumbImg = new Image { image = thumb, scaleMode = ScaleMode.ScaleToFit };
            thumbImg.style.width = 60f; thumbImg.style.height = 60f;
            thumbImg.pickingMode = PickingMode.Ignore;
            bar.Add(thumbImg);
        }

        var nameStrip = new VisualElement();
        nameStrip.style.position = Position.Absolute;
        nameStrip.style.left = 0; nameStrip.style.right = 0; nameStrip.style.bottom = 0;
        nameStrip.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
        nameStrip.style.paddingTop = nameStrip.style.paddingBottom = 2f;
        nameStrip.pickingMode = PickingMode.Ignore;
        bar.Add(nameStrip);

        var name = new Label(part.Variant);
        ApplyFont(name, true, 10);
        name.style.color = Color.white;
        name.style.unityTextAlign = TextAnchor.MiddleCenter;
        name.style.whiteSpace = WhiteSpace.Normal;
        name.pickingMode = PickingMode.Ignore;
        nameStrip.Add(name);

        var sub = new Label($"{part.Slot} · {part.Gender}");
        ApplyFont(sub, false, 9);
        sub.style.color = ColSubtleText;
        sub.style.unityTextAlign = TextAnchor.MiddleCenter;
        sub.style.paddingTop = 3f; sub.style.paddingBottom = 3f;
        sub.pickingMode = PickingMode.Ignore;
        card.Add(sub);

        if (!part.MetadataReviewed)
        {
            var badge = new Label("NEW");
            ApplyFont(badge, true, 9);
            badge.style.position = Position.Absolute;
            badge.style.top = 4; badge.style.right = 4;
            badge.style.backgroundColor = ColOrange;
            badge.style.color = ColOrangeText;
            badge.style.paddingLeft = badge.style.paddingRight = 4f;
            badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius =
                badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 4f;
            badge.pickingMode = PickingMode.Ignore;
            card.Add(badge);
        }

        card.RegisterCallback<PointerUpEvent>(e => { if (e.button == 0) SelectPart(lib, part); });
        return card;
    }

    private static Color ColorForSlot(string slot) => slot switch
    {
        "hair" => new Color(0.75f, 0.6f, 0.3f),
        "hat" => ColFireRed,
        "vest" => ColOrange,
        "body" => ColBlue,
        _ => new Color(0.4f, 0.45f, 0.5f),
    };

    // ── "Pimp My Employee" mode — editing one specific live employee's cosmetic slots ──────────
    private void RefreshEmployeeMode()
    {
        var rec = _employeeIdentity?.Record;
        _grid.Clear();
        _employeeCategoryBar.Clear();

        if (rec == null)
        {
            _countLabel.text = "";
            ShowEmployeeUnavailableDetail("No employee selected.");
            return;
        }

        if (_employeeSpawner != null && !_employeeSpawner.UsesModularAvatar(rec))
        {
            _countLabel.text = rec.employeeName;
            _grid.Add(MakeInfoLabel($"{rec.employeeName}'s current look is a fixed model, not a modular avatar — nothing here to swap yet."));
            ShowEmployeeUnavailableDetail("This employee's look isn't modular yet. Clothing and skin color editing for everyone is coming later too.");
            _previewImage.style.display = DisplayStyle.None;
            _previewEmptyHint.style.display = DisplayStyle.Flex;
            _previewTitleLabel.text = rec.employeeName;
            return;
        }

        var lib = ModularAvatarAssembler.LoadLibrary();
        if (lib == null || lib.PartCount == 0)
        {
            _countLabel.text = rec.employeeName;
            _grid.Add(MakeInfoLabel("No parts scanned yet — drop a modular FBX into the drop folder, or click Rescan Folder above."));
            return;
        }

        // First time this employee has been opened this session — seed from their ACTUAL current
        // look (re-derived deterministically from their stable seed) rather than starting blank,
        // so every category shows what's really on them before any edits are made.
        if (_pendingOverrides.Count == 0)
            SeedPendingOverridesFromCurrentLook(rec, lib);

        // Category tabs — single-select, built by hand (not the reusable Chip class, which
        // self-toggles on click; that would fight the full rebuild this method already does).
        foreach (var (key, label) in EmployeeCategories)
        {
            bool selected = key == _employeeCategory;
            var tab = new Label(label) { name = "aod-employee-tab" };
            ApplyFont(tab, true, 12);
            tab.style.paddingLeft = tab.style.paddingRight = 12f;
            tab.style.paddingTop = tab.style.paddingBottom = 6f;
            tab.style.marginRight = 6f; tab.style.marginBottom = 6f;
            tab.style.borderTopLeftRadius = tab.style.borderTopRightRadius =
                tab.style.borderBottomLeftRadius = tab.style.borderBottomRightRadius = 14f;
            tab.style.borderTopWidth = tab.style.borderBottomWidth = tab.style.borderLeftWidth = tab.style.borderRightWidth = 2f;
            tab.style.backgroundColor = selected ? ColOrange : ColPanelLight;
            tab.style.borderTopColor = tab.style.borderBottomColor = tab.style.borderLeftColor = tab.style.borderRightColor
                = selected ? ColOrangeEdge : ColBlueEdge;
            tab.style.color = selected ? ColOrangeText : ColSubtleText;
            tab.pickingMode = PickingMode.Position;
            AddPressFeedback(tab, 1.06f, 0.95f);
            tab.RegisterCallback<PointerUpEvent>(e => { if (e.button == 0) { _employeeCategory = key; RefreshEmployeeMode(); } });
            _employeeCategoryBar.Add(tab);
        }

        string gender = rec.gender == EmployeeGender.Female ? "female" : "male";
        var (slot, matches) = ModularAvatarAssembler.OverrideCategoryInfo(_employeeCategory);
        var options = lib.VariantsFor(gender, slot).Where(matches).OrderBy(p => p.Variant).ToList();
        string catLabel = EmployeeCategories.First(c => c.key == _employeeCategory).label;

        _countLabel.text = $"{rec.employeeName} — {catLabel}";
        _grid.Add(BuildEmployeeNoneCard());
        foreach (var part in options)
            _grid.Add(BuildEmployeeOptionCard(lib, part));

        RebuildEmployeePreview(rec, lib);
        BuildEmployeeDetailPanel(rec);
    }

    private VisualElement MakeInfoLabel(string text)
    {
        var l = new Label(text);
        ApplyFont(l, false, 13);
        l.style.color = ColSubtleText;
        l.style.whiteSpace = WhiteSpace.Normal;
        return l;
    }

    private void ShowEmployeeUnavailableDetail(string message)
    {
        _detailPanel.Clear();
        var hint = new Label(message);
        ApplyFont(hint, false, 13);
        hint.style.color = ColSubtleText;
        hint.style.whiteSpace = WhiteSpace.Normal;
        hint.style.paddingLeft = hint.style.paddingRight = hint.style.paddingTop = 14f;
        _detailPanel.Add(hint);
    }

    /// <summary>Re-derives what's ACTUALLY currently on this employee by rebuilding with their real
    /// seed and already-persisted overrides, then reads back the resulting per-category picks. The
    /// instance itself is thrown away immediately — only <c>chosen</c> is needed here.</summary>
    private void SeedPendingOverridesFromCurrentLook(EmployeeRecord rec, AvatarPartLibrary lib)
    {
        string gender = rec.gender == EmployeeGender.Female ? "female" : "male";
        int seed = ModularAvatarAssembler.StableSeed(rec.employeeGuid);
        var probe = ModularAvatarAssembler.Build(lib, gender, new System.Random(seed), rec.role,
            rec.AvatarOverridesDict(), out var chosen);
        if (probe != null) UnityEngine.Object.Destroy(probe);

        foreach (var key in ModularAvatarAssembler.EditableOverrideKeys)
            _pendingOverrides[key] = chosen.TryGetValue(key, out var p) && p != null ? p.ObjectName : "";
    }

    /// <summary>Rebuilds the full assembled avatar with the current (unsaved) pending overrides and
    /// shows it on the big rotatable preview — called after every option click so the preview
    /// always reflects what would be applied.</summary>
    private void RebuildEmployeePreview(EmployeeRecord rec, AvatarPartLibrary lib)
    {
        string gender = rec.gender == EmployeeGender.Female ? "female" : "male";
        int seed = ModularAvatarAssembler.StableSeed(rec.employeeGuid);
        var avatar = ModularAvatarAssembler.Build(lib, gender, new System.Random(seed), rec.role, _pendingOverrides, out _);

        _previewTitleLabel.text = rec.employeeName;
        if (avatar != null)
        {
            _previewImage.style.display = DisplayStyle.Flex;
            _previewEmptyHint.style.display = DisplayStyle.None;
            AODPreviewStage.ShowAssembledInstance(avatar); // stage adopts + owns the instance now
        }
        else
        {
            _previewImage.style.display = DisplayStyle.None;
            _previewEmptyHint.style.display = DisplayStyle.Flex;
        }
    }

    private VisualElement BuildEmployeeNoneCard()
    {
        var card = new VisualElement { name = "aod-card" };
        card.style.width = 108f;
        card.style.height = 82f;
        card.style.marginRight = 8f;
        card.style.marginBottom = 8f;
        card.style.backgroundColor = ColCellOdd;
        bool selected = _pendingOverrides.TryGetValue(_employeeCategory, out var v) && string.IsNullOrEmpty(v);
        card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = 2f;
        var borderCol = selected ? ColOrange : ColBlueEdge;
        card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = borderCol;
        card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 6f;
        card.style.justifyContent = Justify.Center;
        card.style.alignItems = Align.Center;
        AddPressFeedback(card, 1.06f, 0.97f);

        var label = new Label("None");
        ApplyFont(label, true, 12);
        label.style.color = ColSubtleText;
        label.pickingMode = PickingMode.Ignore;
        card.Add(label);

        card.RegisterCallback<PointerUpEvent>(e =>
        {
            if (e.button != 0) return;
            _pendingOverrides[_employeeCategory] = "";
            RefreshEmployeeMode();
        });
        return card;
    }

    private VisualElement BuildEmployeeOptionCard(AvatarPartLibrary lib, IAvatarPart part)
    {
        var card = new VisualElement { name = "aod-card" };
        card.style.width = 108f;
        card.style.marginRight = 8f;
        card.style.marginBottom = 8f;
        card.style.backgroundColor = ColCellEven;
        bool selected = _pendingOverrides.TryGetValue(_employeeCategory, out var v) && v == part.ObjectName;
        card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = 2f;
        var borderCol = selected ? ColOrange : ColBlueEdge;
        card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = borderCol;
        card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 6f;
        card.style.overflow = Overflow.Hidden;
        AddPressFeedback(card, 1.06f, 0.97f);

        var bar = new VisualElement();
        bar.style.height = 82f;
        bar.style.backgroundColor = ColorForSlot(part.Slot);
        bar.style.justifyContent = Justify.Center;
        bar.style.alignItems = Align.Center;
        bar.pickingMode = PickingMode.Ignore;
        card.Add(bar);

        var thumb = AODPreviewStage.GetOrCaptureThumbnail(lib, part);
        if (thumb != null)
        {
            var thumbImg = new Image { image = thumb, scaleMode = ScaleMode.ScaleToFit };
            thumbImg.style.width = 60f; thumbImg.style.height = 60f;
            thumbImg.pickingMode = PickingMode.Ignore;
            bar.Add(thumbImg);
        }

        var nameStrip = new VisualElement();
        nameStrip.style.position = Position.Absolute;
        nameStrip.style.left = 0; nameStrip.style.right = 0; nameStrip.style.bottom = 0;
        nameStrip.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
        nameStrip.style.paddingTop = nameStrip.style.paddingBottom = 2f;
        nameStrip.pickingMode = PickingMode.Ignore;
        bar.Add(nameStrip);

        var name = new Label(part.Variant);
        ApplyFont(name, true, 10);
        name.style.color = Color.white;
        name.style.unityTextAlign = TextAnchor.MiddleCenter;
        name.style.whiteSpace = WhiteSpace.Normal;
        name.pickingMode = PickingMode.Ignore;
        nameStrip.Add(name);

        card.RegisterCallback<PointerUpEvent>(e =>
        {
            if (e.button != 0) return;
            _pendingOverrides[_employeeCategory] = part.ObjectName;
            RefreshEmployeeMode();
        });
        return card;
    }

    private void BuildEmployeeDetailPanel(EmployeeRecord rec)
    {
        _detailPanel.Clear();

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = 1f;
        scroll.style.paddingLeft = scroll.style.paddingRight = scroll.style.paddingTop = 14f;
        _detailPanel.Add(scroll);

        var header = new Label(rec.employeeName);
        ApplyFont(header, true, 18);
        header.style.color = ColTitleText;
        header.style.whiteSpace = WhiteSpace.Normal;
        scroll.Add(header);

        var subLabel = new Label(rec.role.DisplayName());
        ApplyFont(subLabel, false, 12);
        subLabel.style.color = ColSubtleText;
        subLabel.style.marginBottom = 12f;
        scroll.Add(subLabel);

        Label Section(string text)
        {
            var l = new Label(text);
            ApplyFont(l, true, 12);
            l.style.color = ColSubtleText;
            l.style.marginTop = 10f;
            l.style.marginBottom = 4f;
            scroll.Add(l);
            return l;
        }

        Section("CURRENT LOADOUT");
        foreach (var (key, label) in EmployeeCategories)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginBottom = 4f } };
            var lbl = new Label(label);
            ApplyFont(lbl, false, 12);
            lbl.style.color = ColSubtleText;
            row.Add(lbl);

            string current = _pendingOverrides.TryGetValue(key, out var v) ? v : "";
            var val = new Label(string.IsNullOrEmpty(current) ? "None" : current);
            ApplyFont(val, true, 12);
            val.style.color = ColTitleText;
            val.style.whiteSpace = WhiteSpace.Normal;
            row.Add(val);

            scroll.Add(row);
        }

        var hint = new Label("Clothing and skin color aren't editable yet — coming later.");
        ApplyFont(hint, false, 11);
        hint.style.color = ColSubtleText;
        hint.style.whiteSpace = WhiteSpace.Normal;
        hint.style.marginTop = 10f;
        scroll.Add(hint);

        var applyBtn = MakeButton("Apply to Employee", ColGreen, ColGreenEdge, ColGreen, ColVanilla, 14);
        applyBtn.style.marginTop = 16f;
        applyBtn.style.height = 40f;
        applyBtn.clicked += () => ApplyEmployeeOverrides(rec);
        scroll.Add(applyBtn);

        var closeBtn = MakeButton("Close", ColBlue, ColBlueEdge, ColBlueHover, ColVanilla, 14);
        closeBtn.style.marginTop = 8f;
        closeBtn.style.height = 36f;
        closeBtn.clicked += Hide;
        scroll.Add(closeBtn);
    }

    private void ApplyEmployeeOverrides(EmployeeRecord rec)
    {
        foreach (var key in ModularAvatarAssembler.EditableOverrideKeys)
            if (_pendingOverrides.TryGetValue(key, out var v)) rec.SetAvatarOverride(key, v);

        if (_employeeSpawner != null && _employeeIdentity != null)
            _employeeSpawner.RefreshAvatarAppearance(_employeeIdentity);

        UIToast.Show($"{rec.employeeName}'s look updated.");
    }

    // ── Detail panel (right-hand metadata editor) ──
    private void ShowEmptyDetail()
    {
        _detailPanel.Clear();
        _previewImage.style.display = DisplayStyle.None;
        _previewEmptyHint.style.display = DisplayStyle.Flex;
        _previewTitleLabel.text = string.Empty;

        var hint = new Label("Select an item to view and edit its details.");
        ApplyFont(hint, false, 13);
        hint.style.color = ColSubtleText;
        hint.style.whiteSpace = WhiteSpace.Normal;
        hint.style.paddingLeft = hint.style.paddingRight = hint.style.paddingTop = 14f;
        _detailPanel.Add(hint);
    }

    private void SelectPart(AvatarPartLibrary lib, IAvatarPart part)
    {
        _selectedPart = part;
        Refresh(); // re-draw grid so the newly-selected card's border highlights

        _previewImage.style.display = DisplayStyle.Flex;
        _previewEmptyHint.style.display = DisplayStyle.None;
        _previewTitleLabel.text = part.ObjectName;
        AODPreviewStage.ShowPart(lib, part);

        BuildDetailPanel(lib, part);
    }

    private void BuildDetailPanel(AvatarPartLibrary lib, IAvatarPart part)
    {
        _detailPanel.Clear();

        // Everything below lives in ONE scroll region per Tad's spec ("the whole right side is
        // going to need one big scroll bar") — no separate fixed header above it.
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = 1f;
        scroll.style.paddingLeft = scroll.style.paddingRight = scroll.style.paddingTop = 14f;
        _detailPanel.Add(scroll);

        Label Section(string text)
        {
            var l = new Label(text);
            ApplyFont(l, true, 12);
            l.style.color = ColSubtleText;
            l.style.marginTop = 10f;
            l.style.marginBottom = 4f;
            scroll.Add(l);
            return l;
        }

        // Slot is derived from the folder scan and intentionally NOT editable here — changing it
        // would change which bone the assembler attaches this part to, which is a Blender-side
        // authoring decision, not a metadata tweak. Shown read-only for context.
        var slotLabel = new Label("Slot: " + part.Slot);
        ApplyFont(slotLabel, false, 12);
        slotLabel.style.color = ColSubtleText;
        scroll.Add(slotLabel);

        Section("GENDER");
        var genderRow = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
        scroll.Add(genderRow);
        foreach (var g in new[] { "male", "female", "neutral" })
        {
            var chip = new Chip(g.Substring(0, 1).ToUpper() + g.Substring(1));
            chip.SetSelected(part.Gender == g, silent: true);
            genderRow.Add(chip.Root);
        }
        // Single-select behavior for the gender row: picking one deselects the others.
        var genderChipsList = genderRow.Children().ToList();
        for (int i = 0; i < genderChipsList.Count; i++)
        {
            var idx = i;
            genderRow[idx].RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.button != 0) return;
                for (int j = 0; j < genderChipsList.Count; j++)
                {
                    var lbl = ((Label)genderRow[j][0]).text.ToLower();
                    bool sel = j == idx;
                    genderRow[j].style.backgroundColor = sel ? ColOrange : ColPanelLight;
                    genderRow[j].style.borderTopColor = genderRow[j].style.borderBottomColor =
                        genderRow[j].style.borderLeftColor = genderRow[j].style.borderRightColor = sel ? ColOrangeEdge : ColBlueEdge;
                    ((Label)genderRow[j][0]).style.color = sel ? ColOrangeText : ColSubtleText;
                    if (sel) part.Gender = lbl;
                }
            }, TrickleDown.TrickleDown);
        }

        Section("ALLOWED ROLES  (none selected = every role)");
        var roleRow = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
        scroll.Add(roleRow);
        foreach (EmployeeRole role in Enum.GetValues(typeof(EmployeeRole)))
        {
            var chip = new Chip(role.DisplayName());
            chip.SetSelected(part.AllowedRoles.Contains(role), silent: true);
            chip.OnChanged += selected =>
            {
                if (selected) { if (!part.AllowedRoles.Contains(role)) part.AllowedRoles.Add(role); }
                else part.AllowedRoles.Remove(role);
            };
            roleRow.Add(chip.Root);
        }

        Section("DEFAULT WEIGHT  (relative odds when no specific rule applies)");
        var weightRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
        scroll.Add(weightRow);
        var weightSlider = new Slider(0f, 100f) { value = Mathf.Clamp(part.DefaultWeight, 0f, 100f) };
        weightSlider.style.flexGrow = 1f;
        weightRow.Add(weightSlider);
        var weightValueLabel = new Label($"{Mathf.RoundToInt(weightSlider.value)}%");
        ApplyFont(weightValueLabel, true, 12);
        weightValueLabel.style.color = ColTitleText;
        weightValueLabel.style.width = 44f;
        weightValueLabel.style.marginLeft = 8f;
        weightValueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        weightRow.Add(weightValueLabel);
        weightSlider.RegisterValueChangedCallback(e =>
        {
            part.DefaultWeight = e.newValue;
            weightValueLabel.text = $"{Mathf.RoundToInt(e.newValue)}%";
        });

        // Skyrim-style clipping fix (2026-09-30) — only clothing items get to hide a body part; a
        // body part itself (torso/head/arms/etc) has no "worn over" semantics, so this section is
        // hidden for those to avoid implying a body part could hide itself.
        if (!ModularAvatarAssembler.IsBodySlot(part.Slot))
        {
            Section("HIDES BODY PARTS WHEN WORN  (prevents clipping — Head always stays visible)");
            var hideRow = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            scroll.Add(hideRow);
            foreach (var bodySlot in BodySlotChipOrder)
            {
                var chip = new Chip(BodySlotLabel(bodySlot));
                chip.SetSelected(part.HiddenBodySlots.Contains(bodySlot), silent: true);
                chip.OnChanged += selected =>
                {
                    if (selected) { if (!part.HiddenBodySlots.Contains(bodySlot)) part.HiddenBodySlots.Add(bodySlot); }
                    else part.HiddenBodySlots.Remove(bodySlot);
                };
                hideRow.Add(chip.Root);
            }
        }

        Section("COLOR VARIANTS  (click a swatch to add/remove it)");
        var paletteGrid = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
        scroll.Add(paletteGrid);
        var variantCountLabel = new Label();
        ApplyFont(variantCountLabel, false, 11);
        variantCountLabel.style.color = ColSubtleText;
        variantCountLabel.style.marginTop = 4f;
        scroll.Add(variantCountLabel);

        void RefreshVariantCount() =>
            variantCountLabel.text = part.ColorVariants.Count == 0
                ? "Using the part's own authored color (no variants selected)."
                : $"{part.ColorVariants.Count} color variant(s) selected.";

        var palette = LoadSimplePalette();
        if (palette.Count == 0)
        {
            var missing = new Label("Palette texture not found (Assets/polyperfect/Common/Textures/atlas-albedo-LPAP.png).");
            ApplyFont(missing, false, 11);
            missing.style.color = ColSubtleText;
            missing.style.whiteSpace = WhiteSpace.Normal;
            paletteGrid.Add(missing);
        }
        foreach (var swatchColor in palette)
        {
            var sw = new VisualElement { name = "aod-swatch" };
            sw.style.width = 26f; sw.style.height = 26f;
            sw.style.marginRight = 3f; sw.style.marginBottom = 3f;
            sw.style.backgroundColor = swatchColor;
            sw.style.borderTopLeftRadius = sw.style.borderTopRightRadius =
                sw.style.borderBottomLeftRadius = sw.style.borderBottomRightRadius = 3f;
            sw.pickingMode = PickingMode.Position;
            AddPressFeedback(sw, 1.15f, 0.9f);

            void ApplySwatchBorder()
            {
                bool selected = part.ColorVariants.Any(cv => ColorsClose(cv.color, swatchColor));
                sw.style.borderTopWidth = sw.style.borderBottomWidth =
                    sw.style.borderLeftWidth = sw.style.borderRightWidth = selected ? 3f : 1f;
                var borderColor = selected ? ColOrange : new Color(0f, 0f, 0f, 0.4f);
                sw.style.borderTopColor = sw.style.borderBottomColor =
                    sw.style.borderLeftColor = sw.style.borderRightColor = borderColor;
            }
            ApplySwatchBorder();

            sw.RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.button != 0) return;
                var existing = part.ColorVariants.FirstOrDefault(cv => ColorsClose(cv.color, swatchColor));
                if (existing != null) part.ColorVariants.Remove(existing);
                else part.ColorVariants.Add(new AvatarPartLibrary.ColorVariant
                {
                    name = "#" + ColorUtility.ToHtmlStringRGB(swatchColor),
                    color = swatchColor,
                });
                ApplySwatchBorder();
                RefreshVariantCount();
            });

            paletteGrid.Add(sw);
        }
        RefreshVariantCount();

        // ── Validation error banner — hidden unless a Submit/Update attempt was rejected. Kept as a
        // field so Submit() can show it without a full BuildDetailPanel rebuild (which would also
        // discard whatever the user was mid-editing above). ──
        _submitErrorLabel = new Label { style = { display = DisplayStyle.None } };
        ApplyFont(_submitErrorLabel, true, 12);
        _submitErrorLabel.style.color = ColVanilla;
        _submitErrorLabel.style.backgroundColor = ColFireRed;
        _submitErrorLabel.style.borderTopWidth = _submitErrorLabel.style.borderBottomWidth =
            _submitErrorLabel.style.borderLeftWidth = _submitErrorLabel.style.borderRightWidth = 2f;
        _submitErrorLabel.style.borderTopColor = _submitErrorLabel.style.borderBottomColor =
            _submitErrorLabel.style.borderLeftColor = _submitErrorLabel.style.borderRightColor = ColFireRedEdge;
        _submitErrorLabel.style.borderTopLeftRadius = _submitErrorLabel.style.borderTopRightRadius =
            _submitErrorLabel.style.borderBottomLeftRadius = _submitErrorLabel.style.borderBottomRightRadius = 6f;
        _submitErrorLabel.style.paddingLeft = _submitErrorLabel.style.paddingRight =
            _submitErrorLabel.style.paddingTop = _submitErrorLabel.style.paddingBottom = 8f;
        _submitErrorLabel.style.marginTop = 12f;
        _submitErrorLabel.style.whiteSpace = WhiteSpace.Normal;
        scroll.Add(_submitErrorLabel);

        // ── Submit/Update ──
        var submitBtn = MakeButton(part.MetadataReviewed ? "Update" : "Submit to AOD", ColGreen, ColGreenEdge, ColGreen, ColVanilla, 14);
        submitBtn.style.marginTop = 8f;
        submitBtn.style.marginBottom = 16f;
        submitBtn.style.height = 40f;
        submitBtn.clicked += () => Submit(lib, part);
        scroll.Add(submitBtn);
    }

    // Body slots offered as hide-targets in the clipping-fix chip row above — deliberately excludes
    // "head" (never a valid hide target, see ApplyBodyPartMasking) and keeps the rest in the same
    // top-to-bottom order Tad described the body: Neck, Torso, Arms, Hands, Waist, Legs, Feet.
    private static readonly string[] BodySlotChipOrder = { "neck", "body", "arms", "hands", "waist", "legs", "feet" };

    private static string BodySlotLabel(string slot) => slot switch
    {
        "body" => "Torso",
        "neck" => "Neck",
        "arms" => "Arms",
        "hands" => "Hands",
        "waist" => "Waist",
        "legs" => "Legs",
        "feet" => "Feet",
        _ => slot,
    };

    /// <summary>Approximate equality for palette-derived colors — exact float comparison is fine
    /// in practice since both sides ultimately come from the same cached palette list, but a small
    /// epsilon guards against any float round-tripping through Color32/hex conversion.</summary>
    private static bool ColorsClose(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

    private static List<Color> _paletteCache;

    /// <summary>Loads the FLAT (non-gradient) Polyperfect atlas — Tad: "the perfect poly UV map, the
    /// simple one not the gradient one". atlas-source-LPAP/atlas-gradient-LPAP both carry a vertical
    /// shading gradient per cell (that gradient is what caused the original material-revert bug this
    /// project fought at the very start); atlas-albedo-LPAP is the same 8x8 grid with flat, unshaded
    /// colors — sampled from the CENTER of each cell so cell-boundary anti-aliasing never leaks in.
    /// Read via raw file bytes + ImageConversion rather than AssetDatabase.LoadAssetAtPath, so this
    /// works regardless of the source asset's own Read/Write Enabled import setting (never touches
    /// that setting, since it's a shared asset other materials also reference).</summary>
    private static List<Color> LoadSimplePalette()
    {
        if (_paletteCache != null) return _paletteCache;
        _paletteCache = new List<Color>();
#if UNITY_EDITOR
        const string path = "Assets/polyperfect/Common/Textures/atlas-albedo-LPAP.png";
        if (!File.Exists(path)) return _paletteCache;

        var bytes = File.ReadAllBytes(path);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (ImageConversion.LoadImage(tex, bytes))
        {
            const int cols = 8, rows = 8;
            int cw = tex.width / cols, ch = tex.height / rows;
            // Row 0 in Unity texture space is the BOTTOM of the image, but the palette should list
            // top-to-bottom the way it visually reads — walk rows highest-to-lowest.
            for (int r = rows - 1; r >= 0; r--)
                for (int c = 0; c < cols; c++)
                    _paletteCache.Add(tex.GetPixel(c * cw + cw / 2, r * ch + ch / 2));
        }
        UnityEngine.Object.Destroy(tex);
#endif
        return _paletteCache;
    }

    /// <summary>Validates and finalizes a part via ModularAvatarFinalizer (an Editor-only assembly
    /// this runtime-UI assembly has no reference to — reached by reflection, same pattern as the
    /// "Rescan Folder" button above). A raw Part gets finalized into a new AvatarPartAsset+prefab
    /// pair; an already-finalized AvatarPartAsset re-validates against its current raw source (if any
    /// still exists) and overwrites the same prefab/asset in place. On failure, shows the returned
    /// error in the detail panel's banner instead of silently doing nothing — a broken part can no
    /// longer go live without an explicit, visible rejection.</summary>
    private void Submit(AvatarPartLibrary lib, IAvatarPart part)
    {
#if UNITY_EDITOR
        var finalizerType = System.AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return System.Type.EmptyTypes; } })
            .FirstOrDefault(t => t.Name == "ModularAvatarFinalizer");
        if (finalizerType == null)
        {
            ShowSubmitError("Could not find ModularAvatarFinalizer via reflection.");
            return;
        }

        if (part is AvatarPartLibrary.Part rawPart)
        {
            var method = finalizerType.GetMethod("TryFinalize", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            object[] args = { lib, rawPart, null, null };
            bool ok = (bool)method.Invoke(null, args);
            if (!ok)
            {
                ShowSubmitError((string)args[2]);
                return;
            }
            var newAsset = (AvatarPartAsset)args[3];
            lib.parts.Remove(rawPart);
            lib.finalizedParts.Add(newAsset);
            UnityEditor.EditorUtility.SetDirty(lib);
            UnityEditor.AssetDatabase.SaveAssets();
            AODPreviewStage.ClearThumbnailCache(); // the finalized prefab may render slightly differently from the raw source
            _selectedPart = newAsset;
        }
        else if (part is AvatarPartAsset asset)
        {
            var method = finalizerType.GetMethod("TryUpdateFromRawSource", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            object[] args = { lib, asset, null };
            bool ok = (bool)method.Invoke(null, args);
            if (!ok)
            {
                ShowSubmitError((string)args[2]);
                return;
            }
            AODPreviewStage.ClearThumbnailCache();
        }

        // Per Tad (2026-09-30): a Submit/Update should be felt everywhere live, not just on the
        // next hire — rebuild every already-spawned modular-avatar employee and recapture their
        // portrait so the hiring board / employee info UI stop showing a stale look. Play-mode only:
        // there's nothing spawned to refresh in the editor outside Play, and FindFirstObjectByType
        // would otherwise just silently no-op anyway.
        if (Application.isPlaying)
        {
            if (_employeeSpawner == null) _employeeSpawner = UnityEngine.Object.FindFirstObjectByType<EmployeeSpawner>();
            _employeeSpawner?.RefreshAllModularAvatars();
        }

        Refresh();
        BuildDetailPanel(lib, _selectedPart); // re-render so the button label flips to "Update" and the NEW badge disappears from view
#else
        ShowSubmitError("The AOD is an editor-only tool.");
#endif
    }

    private void ShowSubmitError(string message)
    {
        if (_submitErrorLabel == null) return;
        _submitErrorLabel.text = string.IsNullOrEmpty(message) ? "Submit failed for an unknown reason." : message;
        _submitErrorLabel.style.display = DisplayStyle.Flex;
    }
}
