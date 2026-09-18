using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Inventory;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Authoring tool for creating/editing <see cref="SkuData"/> assets and their case prefabs, with a
/// live rotating 3D pallet preview (case on a CHEP pallet, built the same way <see cref="PalletBuilder"/>
/// builds a real one). Two workflows: "New Item" auto-numbers a fresh SKU; "Edit Existing Item" loads
/// one of the SKUs already under Resources/Inventory/SKUs for editing.
///
/// Writing SkuData/prefab assets to disk requires AssetDatabase, which only exists in the Editor — same
/// constraint every other asset-authoring tool in this codebase already has (CaseGeneratorTool is
/// Editor-only outright). This panel is a runtime UI Toolkit panel (so it can show the live 3D preview
/// while the game is in Play mode, same as the rest of the UI_UX panels), but Submit/Delete/prefab
/// generation are gated behind UNITY_EDITOR, matching PalletBuilder.cs's own inline convention.
///
/// Has no number-key slot of its own — every digit 0/5-9 is already taken by another panel, and 1-4
/// belong to Dev Console/Hiring Board/Employee Roster/Employee List. Opened via a small "Items" button
/// TopBarUI adds to the top bar, and registered as an AUXILIARY panel (see UIKeyBindingManager) so Tab
/// and Escape still close it like every other panel even without a hotkey of its own.
/// </summary>
public class ItemCreatorPanel : IUIPanel
{
    // ── Palette (each panel in this project declares its own — see ContractsPanel's doc comment) ──
    private static readonly Color ColBg          = new Color(18f / 255f, 26f / 255f, 36f / 255f, 0.97f);
    private static readonly Color ColBorder      = new Color(0x5C / 255f, 0x9B / 255f, 0xC4 / 255f, 1f);
    private static readonly Color ColTitleText   = new Color(0xCF / 255f, 0xE2 / 255f, 0xF0 / 255f, 1f);
    private static readonly Color ColSubtleText  = new Color(0x7A / 255f, 0x99 / 255f, 0xB0 / 255f, 1f);
    private static readonly Color ColSectionBg   = new Color(0.10f, 0.14f, 0.19f, 0.9f);
    private static readonly Color ColSectionEdge = new Color(0x2C / 255f, 0x5E / 255f, 0x82 / 255f, 1f);
    private static readonly Color ColOrange      = new Color(0xB5 / 255f, 0x74 / 255f, 0x3A / 255f, 1f);
    private static readonly Color ColOrangeEdge  = new Color(0x7A / 255f, 0x4C / 255f, 0x22 / 255f, 1f);
    private static readonly Color ColOrangeText  = new Color(0xFD / 255f, 0xE8 / 255f, 0xCC / 255f, 1f);
    private static readonly Color ColOrangeHover = new Color(0xC6 / 255f, 0x7F / 255f, 0x42 / 255f, 1f);
    private static readonly Color ColBlueBtn     = new Color(0x4C / 255f, 0x90 / 255f, 0xC0 / 255f, 1f);
    private static readonly Color ColBlueEdge    = new Color(0x2C / 255f, 0x5E / 255f, 0x82 / 255f, 1f);
    private static readonly Color ColBlueHover   = new Color(0x5B / 255f, 0xA6 / 255f, 0xD8 / 255f, 1f);
    private static readonly Color ColRed         = new Color(0x8E / 255f, 0x2B / 255f, 0x2B / 255f, 1f);
    private static readonly Color ColRedHover    = new Color(0xC0 / 255f, 0x3A / 255f, 0x3A / 255f, 1f);
    private static readonly Color ColDisabledBg  = new Color(0.08f, 0.10f, 0.13f, 0.7f);

    private const float MetersToInches = 39.3701f;
    private const float InchesToMeters = 1f / MetersToInches;
    private const float PalletDeckHeight = 0.165f;
    private const float PreviewLayerGap = 0.01f; // vertical gap between case layers, preview-only — see ApplyDimensionScaleToPreview

    private static Font _lilita;

    // ── Chrome ───────────────────────────────────────────────────────────────
    private readonly VisualElement _overlay;
    private readonly VisualElement _modal;
    private ResizableWindow _resizer;
    private DraggableWindow _dragger;
    private Button _scaleButton;
    private bool _visible;

    // ── Preview quality override ────────────────────────────────────────────
    // Checked every URP quality asset in Assets/Settings/ (including the "Good" and "PC" tiers, not just
    // the active "Toaster" one visible in the FPS overlay) — m_AdditionalLightShadowsSupported is 0 in
    // ALL of them project-wide, so a Spot/Point light can never cast a real-time shadow here no matter
    // what per-light settings it's given. Only the single MAIN (Directional) light gets real shadows
    // (m_MainLightShadowsSupported is 1 everywhere), and the game's own Sun already holds that slot —
    // so the preview temporarily takes it over via RenderSettings.sun while the panel is open. MSAA is
    // also disabled in the active Toaster preset (m_MSAA: 1) and only enabled in the "PC" tier (m_MSAA:
    // 4, plus real soft-shadow support), so the panel also temporarily swaps the whole active render
    // pipeline asset to PC_RPAsset while it's open. Both are restored on Hide().
    private Light _previewKeyLight;
    private Light _prevSunLight;
    private bool _restoreSunLight;
    private UnityEngine.Rendering.RenderPipelineAsset _prevRenderPipelineAsset;
    private bool _restoreRenderPipelineAsset;

    // ── Header ───────────────────────────────────────────────────────────────
    private Button _newItemTab;
    private Button _editItemTab;
    private DropdownField _editDropdown;
    private bool _isEditMode;
    private SkuData _editingSku; // non-null only while editing a loaded existing SKU

    // ── Section 1 fields ─────────────────────────────────────────────────────
    private Label _itemNumberLabel;
    private int _currentItemNumber;
    private TextField _descriptionField;
    private FloatField _lengthField, _widthField, _heightField; // values are INCHES — the fields users edit; converted to meters wherever stored/consumed
    private Label _lengthMetric, _widthMetric, _heightMetric; // read-only "0.306m" readouts beside each field
    private FloatField _weightField;
    private EnumField _storageAreaField;
    private FloatField _buyValueField, _sellValueField;
    private IntegerField _shelfLifeField;

    private Toggle _customCaseToggle;
    private VisualElement _existingPrefabRow;
    private VisualElement _customCaseRow;
    private DropdownField _existingPrefabDropdown;
    private readonly Dictionary<string, GameObject> _existingPrefabsByLabel = new();
    // Runtime UI Toolkit has no ColorField (that's an Editor-only control) — colors are picked from a
    // small preset swatch row instead, same palette CaseGeneratorTool uses for its random rolls.
    private Color _boxColor = new Color(0.55f, 0.36f, 0.20f);
    private Color _tapeColor = new Color(0.80f, 0.74f, 0.55f);
    private static readonly Color[] BoxColorPalette =
    {
        new Color(0.55f, 0.36f, 0.20f), // classic brown cardboard
        new Color(0.32f, 0.20f, 0.12f), // dark brown
        new Color(0.72f, 0.58f, 0.40f), // tan / kraft
        new Color(0.88f, 0.87f, 0.83f), // white
        new Color(0.65f, 0.55f, 0.45f), // light grey-brown
        new Color(0.78f, 0.68f, 0.52f), // light tan
        new Color(0.42f, 0.28f, 0.15f), // chocolate brown
        new Color(0.75f, 0.55f, 0.50f), // light red/salmon
    };
    private static readonly Color[] TapeColorPalette =
    {
        new Color(0.80f, 0.74f, 0.55f), // classic tan
        new Color(0.88f, 0.82f, 0.66f), // light tan
        new Color(0.62f, 0.50f, 0.32f), // dark tan
        new Color(0.90f, 0.89f, 0.85f), // white / packing tape
    };
    private VisualElement _iconPickerRow;
    private Sprite _selectedIcon;
    private readonly List<Button> _iconButtons = new();

    // ── Section 2 ────────────────────────────────────────────────────────────
    private VisualElement _section2;
    private IntegerField _tiField, _hiField;
    private Image _previewImage;
    private Label _previewStatusLabel;

    // ── Section 3 ────────────────────────────────────────────────────────────
    private Button _clearButton, _deleteButton, _submitButton;

    // ── Preview rig (real 3D scene objects, rendered into a RenderTexture) ──
    private RenderTexture _previewRT;
    private Camera _previewCamera;
    private GameObject _rigRoot;      // parent for everything, positioned far from the playfield
    private Transform _pivot;         // rotates — what the user drags
    private GameObject _chepInstance;
    private GameObject _caseTemplate; // inactive clone source — either an existing prefab or our procedurally-built one
    private GameObject _liveCaseSingle; // the single-case-on-pallet view shown before "Generate Preview"
    private Vector3 _existingPrefabNativeSizeMeters = Vector3.one; // the selected existing prefab's own mesh size — the "1x scale" reference for ApplyDimensionScaleToPreview
    private PalletBuilder _previewPalletBuilder;
    private bool _dragging;
    private float _lastDragX, _lastDragY;
    // Tracked as explicit yaw/pitch floats (rather than repeated Transform.Rotate calls) so pitch can
    // spring back independently of yaw's continuous auto-spin, and so the two axes never compound into
    // a gimbal-y drift — the pivot's rotation is fully re-derived from these two numbers every tick.
    private float _yawDeg;
    private float _pitchDeg;
    private const float SpinDegPerSec = 12f;
    private const float DragDegPerPixel = 0.45f;
    private const float PitchDragDegPerPixel = 0.35f;
    private const float MaxPitchDeg = 80f;
    private const float PitchReturnLerpPerSec = 3.5f; // slow, subtle spring-back — not instant snap
    // Positive X rotation tips the pivot's local +Z (far side) DOWN, which tips the near side facing
    // the camera (local -Z) UP (verified via Unity's left-handed rotation matrix: point (0,0,-1) maps
    // to (0, sinθ, -cosθ), y-component positive for positive θ). UI Toolkit pointer Y increases
    // DOWNWARD, so dragging up gives a negative dy. To make "drag up" tip the near/front side up
    // (positive pitch), pitch must move opposite dy: pitchDeg -= dy * coef. That's direction = +1f.
    private const float PitchDragDirection = 1f;
    // The ChepEmpty mesh's real footprint sits 90° from PalletBuilder's assumed X=width/Z=length axes —
    // confirmed by hand in the Inspector. Applied to PalletLoad/the single case preview, never to the
    // pallet visual or PalletBuilder's own transform, both of which stay at their authored identity.
    private static readonly Quaternion CasePalletYawCorrection = Quaternion.Euler(0f, 90f, 0f);
    private IVisualElementScheduledItem _tick;

    public bool IsOpen => _visible;

    public ItemCreatorPanel(VisualElement root)
    {
        _overlay = Build(out _modal);
        root.Add(_overlay);
        Hide();

        UIKeyBindingManager.Instance?.RegisterAuxiliary(this);

        EnterNewItemMode();
    }

    public void Toggle() { if (_visible) Hide(); else Show(); }

    public void Show()
    {
        _visible = true;
        _overlay.style.display = DisplayStyle.Flex;
        _overlay.BringToFront();
        UIModalGuard.Push(this);

#if UNITY_EDITOR
        // Swap to the PC-tier pipeline asset (real MSAA + soft shadow support) for as long as the panel
        // is open — see the field comment above for why the active preset alone can't give this.
        var pcAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        if (pcAsset != null && QualitySettings.renderPipeline != pcAsset)
        {
            _prevRenderPipelineAsset = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = pcAsset;
            _restoreRenderPipelineAsset = true;
        }
#endif

        bool rigJustCreated = _rigRoot == null;
        EnsurePreviewRig();
        // The rig is built lazily here, but a case may already have been selected earlier (e.g. the
        // default existing-prefab choice made during construction, before any rig existed) — that
        // selection's ShowSingleCasePreview() call was a silent no-op back then, so re-show it now
        // that there's actually a pivot to parent onto.
        if (rigJustCreated && _caseTemplate != null) ShowSingleCasePreview();
        StartTicking();

        // Steal the "main light" slot for the preview's own key light — only the main directional light
        // gets real shadows in this project (see field comment), and the game's actual Sun already holds
        // it, so the preview's shadow never rendered no matter how the preview light itself was set up.
        if (_previewKeyLight != null)
        {
            _prevSunLight = RenderSettings.sun;
            RenderSettings.sun = _previewKeyLight;
            _restoreSunLight = true;
        }

        // Always opens maximized rather than at normal size — same "FillScreenExact, deferred a frame"
        // pattern as WorkQueuePanel/ContractsPanel. Deferred so there's a real layout to measure on the
        // very first Show() of a session (see ResizableWindow.FillScreen's own doc comment — the same
        // first-call caveat applies to FillScreenExact).
        _overlay.schedule.Execute(() =>
        {
            _resizer?.FillScreenExact();
            if (_scaleButton != null) PanelTitleChrome.SyncScaleGlyph(_scaleButton, _resizer);
        }).ExecuteLater(16);
    }

    public void Hide()
    {
        _visible = false;
        _overlay.style.display = DisplayStyle.None;
        UIModalGuard.Pop(this);

        if (_restoreSunLight)
        {
            RenderSettings.sun = _prevSunLight;
            _restoreSunLight = false;
        }

#if UNITY_EDITOR
        if (_restoreRenderPipelineAsset)
        {
            QualitySettings.renderPipeline = _prevRenderPipelineAsset;
            _restoreRenderPipelineAsset = false;
        }
#endif
    }

    // ── Shell ────────────────────────────────────────────────────────────────

    private VisualElement Build(out VisualElement modal)
    {
        var overlay = new VisualElement { name = "item-creator-overlay" };
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0; overlay.style.top = 0; overlay.style.right = 0; overlay.style.bottom = 0;
        overlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.45f));
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;

        modal = new VisualElement { name = "item-creator-modal" };
        modal.style.width = 920; modal.style.height = 700;
        modal.style.backgroundColor = new StyleColor(ColBg);
        modal.style.borderTopWidth = modal.style.borderBottomWidth =
            modal.style.borderLeftWidth = modal.style.borderRightWidth = 3;
        modal.style.borderTopColor = modal.style.borderBottomColor =
            modal.style.borderLeftColor = modal.style.borderRightColor = new StyleColor(ColBorder);
        modal.style.borderTopLeftRadius = modal.style.borderTopRightRadius =
            modal.style.borderBottomLeftRadius = modal.style.borderBottomRightRadius = 16;
        modal.style.overflow = Overflow.Hidden;
        modal.RegisterCallback<PointerDownEvent>(e => e.StopPropagation()); // don't let clicks fall through to the overlay

        var titleBar = BuildTitleBar(out Button close);
        modal.Add(titleBar);

        var body = new VisualElement { name = "item-creator-body" };
        body.style.flexGrow = 1;
        body.style.paddingLeft = 16; body.style.paddingRight = 16;
        body.style.paddingTop = 10; body.style.paddingBottom = 12;

        body.Add(BuildHeaderRow());
        body.Add(BuildDivider());

        var columns = new VisualElement { name = "item-creator-columns" };
        columns.style.flexDirection = FlexDirection.Row;
        columns.style.flexGrow = 1;
        columns.style.marginTop = 8;

        var leftCol = new ScrollView(ScrollViewMode.Vertical) { name = "item-creator-left" };
        leftCol.style.width = 420; leftCol.style.flexShrink = 0;
        leftCol.style.marginRight = 14;
        leftCol.Add(BuildSection1());
        leftCol.Add(BuildSection2());

        var rightCol = new VisualElement { name = "item-creator-right" };
        rightCol.style.flexGrow = 1;
        rightCol.Add(BuildPreviewFrame());

        columns.Add(leftCol);
        columns.Add(rightCol);
        body.Add(columns);

        body.Add(BuildDivider());
        body.Add(BuildSection3());

        modal.Add(body);

        _resizer = new ResizableWindow(modal, 760f, 560f, 8f, 46f);
        (_scaleButton, _) = PanelTitleChrome.Adopt(close, _resizer, Hide);
        _dragger = new DraggableWindow(modal, titleBar, close);

        overlay.Add(modal);
        return overlay;
    }

    private VisualElement BuildTitleBar(out Button close)
    {
        var bar = new VisualElement { name = "item-creator-titlebar" };
        bar.style.flexDirection = FlexDirection.Row;
        bar.style.alignItems = Align.Center;
        bar.style.height = 44;
        bar.style.paddingLeft = 14; bar.style.paddingRight = 8;
        bar.style.backgroundColor = new StyleColor(new Color(0.12f, 0.16f, 0.24f, 1f));
        bar.style.borderTopLeftRadius = bar.style.borderTopRightRadius = 13;

        var title = MakeText("ITEM CREATOR", 18, ColTitleText, bold: true);
        title.style.flexGrow = 1;
        bar.Add(title);

        close = new Button { text = "✕" };
        bar.Add(close);
        return bar;
    }

    private VisualElement BuildDivider()
    {
        var d = new VisualElement();
        d.style.height = 2;
        d.style.backgroundColor = new StyleColor(ColSectionEdge);
        d.style.marginTop = 4; d.style.marginBottom = 4;
        return d;
    }

    private VisualElement BuildHeaderRow()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginTop = 10;

        _editItemTab = MakeActionButton("Edit Existing Item", ColBlueBtn, ColBlueEdge, ColBlueHover, EnterEditItemMode);
        _editItemTab.style.width = 180;
        _editItemTab.style.fontSize = 17f; // 13 + 30%
        row.Add(_editItemTab);

        _newItemTab = MakeActionButton("Start New Item", ColOrange, ColOrangeEdge, ColOrangeHover, EnterNewItemMode);
        _newItemTab.style.width = 180;
        _newItemTab.style.marginLeft = 8;
        _newItemTab.style.fontSize = 17f; // 13 + 30%
        row.Add(_newItemTab);

        _editDropdown = new DropdownField(new List<string> { "(no items yet)" }, 0);
        _editDropdown.style.flexGrow = 1;
        _editDropdown.style.marginLeft = 12;
        _editDropdown.style.display = DisplayStyle.None;
        _editDropdown.RegisterValueChangedCallback(evt => OnEditDropdownChanged(evt.newValue));
        ApplyFont(_editDropdown, size: 17);
        row.Add(_editDropdown);

        return row;
    }

    // ── Section 1 ────────────────────────────────────────────────────────────

    private VisualElement BuildSection1()
    {
        var section = MakeSectionContainer("1. Setup or choose your case prefab first!", out var content);

        _itemNumberLabel = MakeText("Item Number: —", 13, ColSubtleText, bold: true);
        _itemNumberLabel.style.marginBottom = 8;
        _itemNumberLabel.style.display = DisplayStyle.None; // hidden per request — _currentItemNumber itself still drives Submit/edit logic, only the visible readout is gone
        content.Add(_itemNumberLabel);

        _descriptionField = MakeLabeledText("Item Description", "", v => { });
        content.Add(_descriptionField);

        content.Add(BuildDimensionRow("Case Length (in)", out _lengthField, out _lengthMetric));
        content.Add(BuildDimensionRow("Case Width (in)", out _widthField, out _widthMetric));
        content.Add(BuildDimensionRow("Case Height (in)", out _heightField, out _heightMetric));

        _lengthField.RegisterValueChangedCallback(evt => { _lengthMetric.text = ToMetersLabel(evt.newValue * InchesToMeters); OnDimensionsChanged(); });
        _widthField.RegisterValueChangedCallback(evt => { _widthMetric.text = ToMetersLabel(evt.newValue * InchesToMeters); OnDimensionsChanged(); });
        _heightField.RegisterValueChangedCallback(evt => { _heightMetric.text = ToMetersLabel(evt.newValue * InchesToMeters); OnDimensionsChanged(); });

        _weightField = MakeLabeledFloat("Case Weight (lbs)", 0f, _ => { });
        content.Add(_weightField);

        _storageAreaField = new EnumField("Storage Area", PalletData.AreaCategory.Grocery);
        StyleLabeled(_storageAreaField);
        _storageAreaField.RegisterValueChangedCallback(_ => UpdateShelfLifeVisibility());
        content.Add(_storageAreaField);

        var priceRow = new VisualElement();
        priceRow.style.flexDirection = FlexDirection.Row;
        _buyValueField = MakeLabeledFloat("Buy Value", 0f, _ => { });
        _buyValueField.style.flexGrow = 1;
        SetCompactLabelWidth(_buyValueField);
        _sellValueField = MakeLabeledFloat("Sell Value", 0f, _ => { });
        _sellValueField.style.flexGrow = 1;
        _sellValueField.style.marginLeft = 8;
        SetCompactLabelWidth(_sellValueField);
        priceRow.Add(_buyValueField);
        priceRow.Add(_sellValueField);
        content.Add(priceRow);

        _shelfLifeField = new IntegerField("Shelf Life Days (-1 = non-perishable)") { value = -1 };
        StyleLabeled(_shelfLifeField);
        content.Add(_shelfLifeField);
        UpdateShelfLifeVisibility();

        content.Add(BuildDivider());
        var casePrefabHeader = MakeText("(Optional) Re-do Case Prefab from Scratch?", 13, ColTitleText, bold: true);
        casePrefabHeader.style.fontSize = 19.5f; // match the Shelf Life field label's size (StyleLabeled)
        content.Add(casePrefabHeader);

        _customCaseToggle = new Toggle("Create a custom case instead") { value = false };
        StyleLabeled(_customCaseToggle);
        _customCaseToggle.RegisterValueChangedCallback(evt => SetCustomCaseMode(evt.newValue));
        content.Add(_customCaseToggle);

        _existingPrefabRow = new VisualElement();
        _existingPrefabDropdown = new DropdownField(new List<string> { "(none found)" }, 0);
        ApplyFont(_existingPrefabDropdown, size: 17);
        _existingPrefabDropdown.RegisterValueChangedCallback(evt => OnExistingPrefabChosen(evt.newValue));
        _existingPrefabRow.Add(_existingPrefabDropdown);
        content.Add(_existingPrefabRow);

        _customCaseRow = new VisualElement();
        _customCaseRow.style.display = DisplayStyle.None;

        _customCaseRow.Add(BuildColorSwatchRow("Box Color", BoxColorPalette, _boxColor,
            c => { _boxColor = c; RebuildCustomCasePreview(); }));
        _customCaseRow.Add(BuildColorSwatchRow("Tape Color", TapeColorPalette, _tapeColor,
            c => { _tapeColor = c; RebuildCustomCasePreview(); }));

        _customCaseRow.Add(MakeText("Case Icon", 12, ColSubtleText));
        _iconPickerRow = new VisualElement();
        _iconPickerRow.style.flexDirection = FlexDirection.Row;
        _iconPickerRow.style.flexWrap = Wrap.Wrap;
        _iconPickerRow.style.marginTop = 4; _iconPickerRow.style.marginBottom = 6;
        _customCaseRow.Add(_iconPickerRow);

        var generateCaseButton = MakeActionButton("Generate Case", ColOrange, ColOrangeEdge, ColOrangeHover, RebuildCustomCasePreview);
        _customCaseRow.Add(generateCaseButton);

        content.Add(_customCaseRow);

        return section;
    }

    private VisualElement BuildColorSwatchRow(string label, Color[] palette, Color initial, Action<Color> onSelect)
    {
        var container = new VisualElement();
        container.Add(MakeText(label, 12, ColSubtleText));

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.marginTop = 4; row.style.marginBottom = 6;

        var swatches = new List<VisualElement>();
        foreach (var c in palette)
        {
            var sw = new VisualElement();
            sw.style.width = 28; sw.style.height = 28;
            sw.style.marginRight = 4; sw.style.marginBottom = 4;
            sw.style.backgroundColor = new StyleColor(c);
            sw.style.borderTopWidth = sw.style.borderBottomWidth = sw.style.borderLeftWidth = sw.style.borderRightWidth = 2;
            bool isInitial = ColorsApproxEqual(c, initial);
            sw.style.borderTopColor = sw.style.borderBottomColor = sw.style.borderLeftColor = sw.style.borderRightColor =
                new StyleColor(isInitial ? ColOrangeEdge : ColSectionEdge);

            sw.RegisterCallback<PointerDownEvent>(evt =>
            {
                onSelect(c);
                foreach (var other in swatches)
                    other.style.borderTopColor = other.style.borderBottomColor =
                        other.style.borderLeftColor = other.style.borderRightColor = new StyleColor(ColSectionEdge);
                sw.style.borderTopColor = sw.style.borderBottomColor =
                    sw.style.borderLeftColor = sw.style.borderRightColor = new StyleColor(ColOrangeEdge);
                evt.StopPropagation();
            });

            swatches.Add(sw);
            row.Add(sw);
        }

        container.Add(row);
        return container;
    }

    private static bool ColorsApproxEqual(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.001f && Mathf.Abs(a.g - b.g) < 0.001f && Mathf.Abs(a.b - b.b) < 0.001f;

    private VisualElement BuildDimensionRow(string label, out FloatField field, out Label metric)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;

        field = new FloatField(label) { value = 0f }; // value is INCHES — see field declaration comment
        field.style.flexGrow = 1;
        StyleLabeled(field);
        row.Add(field);

        metric = MakeText("0.0m", 12, ColSubtleText);
        metric.style.width = 64;
        metric.style.marginLeft = 6;
        metric.style.unityTextAlign = TextAnchor.MiddleLeft;
        row.Add(metric);

        return row;
    }

    private static string ToMetersLabel(float meters) => $"{meters:0.0}m";
    private static float RoundToTenth(float v) => Mathf.Round(v * 10f) / 10f;

    private void SetCustomCaseMode(bool custom)
    {
        _existingPrefabRow.style.display = custom ? DisplayStyle.None : DisplayStyle.Flex;
        _customCaseRow.style.display = custom ? DisplayStyle.Flex : DisplayStyle.None;
        if (custom) RebuildCustomCasePreview();
        else OnExistingPrefabChosen(_existingPrefabDropdown?.value);
    }

    /// <summary>Shelf Life only means anything for Perishable/Frozen storage areas — Grocery items are
    /// effectively non-perishable by definition, so the field is hidden rather than just left sitting
    /// there showing "-1" for every Grocery item.</summary>
    private void UpdateShelfLifeVisibility()
    {
        if (_shelfLifeField == null || _storageAreaField == null) return;
        var area = (PalletData.AreaCategory)_storageAreaField.value;
        bool relevant = area == PalletData.AreaCategory.Perishable || area == PalletData.AreaCategory.Frozen;
        _shelfLifeField.style.display = relevant ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // ── Section 2 ────────────────────────────────────────────────────────────

    private VisualElement BuildSection2()
    {
        _section2 = MakeSectionContainer("2. Set the pallet's Ti/Hi and generate a preview", out var content);
        _section2.style.marginTop = 10;

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;

        _tiField = new IntegerField("Ti (cases per layer)") { value = 1 };
        _tiField.style.flexGrow = 1;
        StyleLabeled(_tiField);
        SetCompactLabelWidth(_tiField);
        row.Add(_tiField);

        _hiField = new IntegerField("Hi (layers)") { value = 1 };
        _hiField.style.flexGrow = 1;
        _hiField.style.marginLeft = 8;
        StyleLabeled(_hiField);
        SetCompactLabelWidth(_hiField);
        row.Add(_hiField);

        content.Add(row);

        var generateButton = MakeActionButton("Generate Preview", ColOrange, ColOrangeEdge, ColOrangeHover, OnGeneratePreviewClicked);
        generateButton.style.marginTop = 8;
        content.Add(generateButton);

        _section2.SetEnabled(false);
        return _section2;
    }

    private VisualElement BuildPreviewFrame()
    {
        var frame = new VisualElement { name = "pallet-preview-frame" };
        frame.style.flexGrow = 1;
        frame.style.backgroundColor = new StyleColor(new Color(0.06f, 0.08f, 0.11f, 1f));
        frame.style.borderTopWidth = frame.style.borderBottomWidth =
            frame.style.borderLeftWidth = frame.style.borderRightWidth = 3;
        frame.style.borderTopColor = frame.style.borderBottomColor =
            frame.style.borderLeftColor = frame.style.borderRightColor = new StyleColor(ColOrangeEdge);
        frame.style.borderTopLeftRadius = frame.style.borderTopRightRadius =
            frame.style.borderBottomLeftRadius = frame.style.borderBottomRightRadius = 10;
        frame.style.paddingTop = 8;
        frame.style.alignItems = Align.Center;

        var caption = MakeText("PALLET PREVIEW", 42, ColOrange, bold: true);
        caption.style.marginBottom = 6;
        frame.Add(caption);

        _previewImage = new Image();
        _previewImage.style.flexGrow = 1;
        _previewImage.style.width = Length.Percent(100);
        _previewImage.scaleMode = ScaleMode.ScaleToFit;
        frame.Add(_previewImage);

        _previewImage.RegisterCallback<PointerDownEvent>(OnPreviewPointerDown);
        _previewImage.RegisterCallback<PointerMoveEvent>(OnPreviewPointerMove);
        _previewImage.RegisterCallback<PointerUpEvent>(OnPreviewPointerUp);

        _previewStatusLabel = MakeText("Choose or create a case to begin.", 12, ColSubtleText);
        _previewStatusLabel.style.marginTop = 6; _previewStatusLabel.style.marginBottom = 8;
        frame.Add(_previewStatusLabel);

        return frame;
    }

    // ── Section 3 ────────────────────────────────────────────────────────────

    private VisualElement BuildSection3()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.FlexEnd;
        row.style.marginTop = 4;

        _clearButton = MakeActionButton("Clear", ColBlueBtn, ColBlueEdge, ColBlueHover, OnClearClicked);
        _clearButton.style.width = 120;
        row.Add(_clearButton);

        _deleteButton = MakeActionButton("Delete Item", ColRed, ColRed, ColRedHover, OnDeleteClicked);
        _deleteButton.style.width = 140;
        _deleteButton.style.marginLeft = 8;
        row.Add(_deleteButton);

        _submitButton = MakeActionButton("Submit to Database", ColOrange, ColOrangeEdge, ColOrangeHover, OnSubmitClicked);
        _submitButton.style.width = 190;
        _submitButton.style.marginLeft = 8;
        row.Add(_submitButton);

        return row;
    }

    // ── Mode switching ───────────────────────────────────────────────────────

    private void EnterNewItemMode()
    {
        _isEditMode = false;
        _editingSku = null;
        _editDropdown.style.display = DisplayStyle.None;
        _deleteButton.SetEnabled(false);
        ClearForm();
        PopulateExistingPrefabDropdown();
        _currentItemNumber = GenerateNextItemNumber();
        _itemNumberLabel.text = $"Item Number: {_currentItemNumber} (auto-generated)";
    }

    private void EnterEditItemMode()
    {
        _isEditMode = true;
        _editDropdown.style.display = DisplayStyle.Flex;
        PopulateEditDropdown();
    }

    private void PopulateEditDropdown()
    {
        var skus = Resources.LoadAll<SkuData>("Inventory/SKUs")
            .Where(s => s != null)
            .OrderBy(s => s.ItemNumber)
            .ToList();

        var labels = skus.Select(s => $"{s.ItemNumber} - {s.ItemDescription}").ToList();
        if (labels.Count == 0) labels.Add("(no items yet)");

        _editDropdownSkusByLabel.Clear();
        for (int i = 0; i < skus.Count; i++) _editDropdownSkusByLabel[labels[i]] = skus[i];

        _editDropdown.choices = labels;
        _editDropdown.index = 0;
        if (skus.Count > 0) OnEditDropdownChanged(labels[0]);
    }

    private readonly Dictionary<string, SkuData> _editDropdownSkusByLabel = new();

    private void OnEditDropdownChanged(string label)
    {
        if (label == null || !_editDropdownSkusByLabel.TryGetValue(label, out var sku)) return;
        LoadIntoForm(sku);
    }

    private void LoadIntoForm(SkuData sku)
    {
        _editingSku = sku;
        _deleteButton.SetEnabled(true);

        _currentItemNumber = sku.ItemNumber;
        _itemNumberLabel.text = $"Item Number: {sku.ItemNumber} (existing item)";
        _descriptionField.value = sku.ItemDescription;
        _lengthField.value = RoundToTenth(sku.CaseLength * MetersToInches);
        _widthField.value = RoundToTenth(sku.CaseWidth * MetersToInches);
        _heightField.value = RoundToTenth(sku.CaseHeight * MetersToInches);
        _lengthMetric.text = ToMetersLabel(sku.CaseLength);
        _widthMetric.text = ToMetersLabel(sku.CaseWidth);
        _heightMetric.text = ToMetersLabel(sku.CaseHeight);
        _weightField.value = sku.CaseWeight;
        _storageAreaField.value = sku.StorageArea;
        UpdateShelfLifeVisibility();
        _buyValueField.value = sku.BuyValue;
        _sellValueField.value = sku.SellValue;
        _shelfLifeField.value = sku.ShelfLifeDays;
        _tiField.value = Mathf.Max(1, sku.Ti);
        _hiField.value = Mathf.Max(1, sku.Hi);

        _customCaseToggle.SetValueWithoutNotify(false);
        SetCustomCaseMode(false);

        PopulateExistingPrefabDropdown(sku.Prefab);
        _caseTemplate = sku.Prefab;
        _existingPrefabNativeSizeMeters = GetMeshNativeSizeMeters(sku.Prefab);
        RefreshCaseReadyState();
        ShowSingleCasePreview();
        ApplyDimensionScaleToPreview(); // guarantees the preview matches the SO's saved dimensions the moment the item loads, not just whatever the prefab's own native mesh size is
    }

    private void OnClearClicked()
    {
        ClearForm();
        if (!_isEditMode) _currentItemNumber = GenerateNextItemNumber();
        _itemNumberLabel.text = _isEditMode
            ? "Item Number: — (select an item above)"
            : $"Item Number: {_currentItemNumber} (auto-generated)";
    }

    private void ClearForm()
    {
        _descriptionField.value = string.Empty;
        _lengthField.value = 0f; _widthField.value = 0f; _heightField.value = 0f;
        _lengthMetric.text = "0.0m"; _widthMetric.text = "0.0m"; _heightMetric.text = "0.0m";
        _weightField.value = 0f;
        _storageAreaField.value = PalletData.AreaCategory.Grocery;
        UpdateShelfLifeVisibility();
        _buyValueField.value = 0f; _sellValueField.value = 0f;
        _shelfLifeField.value = -1;
        _tiField.value = 1; _hiField.value = 1;
        _customCaseToggle.SetValueWithoutNotify(false);
        SetCustomCaseMode(false);
        _selectedIcon = null;
        HighlightSelectedIcon();
        _caseTemplate = null;
        RefreshCaseReadyState();
        ClearPreview();
        _previewStatusLabel.text = "Choose or create a case to begin.";
    }

    // ── Case selection (existing prefab) ─────────────────────────────────────

    private void PopulateExistingPrefabDropdown(GameObject preselect = null)
    {
#if UNITY_EDITOR
        _existingPrefabsByLabel.Clear();
        var guids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/_Project/Prefabs/Inventory/Cases" });
        var labels = new List<string>();
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            string label = System.IO.Path.GetFileNameWithoutExtension(path);
            labels.Add(label);
            _existingPrefabsByLabel[label] = go;
        }
        labels.Sort();
        if (labels.Count == 0) labels.Add("(none found)");
        _existingPrefabDropdown.choices = labels;

        string selectLabel = null;
        if (preselect != null)
            selectLabel = _existingPrefabsByLabel.FirstOrDefault(kv => kv.Value == preselect).Key;

        _existingPrefabDropdown.SetValueWithoutNotify(selectLabel ?? labels[0]);
        OnExistingPrefabChosen(_existingPrefabDropdown.value);
#else
        _previewStatusLabel.text = "Case prefab browsing is Editor-only.";
#endif
    }

    private void OnExistingPrefabChosen(string label)
    {
        if (_customCaseToggle.value) return; // custom mode owns the template while active
        if (label != null && _existingPrefabsByLabel.TryGetValue(label, out var go))
        {
            _caseTemplate = go;
            _existingPrefabNativeSizeMeters = GetMeshNativeSizeMeters(go);
            AutoFillDimensionsFromPrefab(go);
            RefreshCaseReadyState();
            ShowSingleCasePreview();
            ApplyDimensionScaleToPreview();
        }
    }

    /// <summary>Picking an existing case prefab doesn't tell you its real-world size, and Generate
    /// Preview needs real dimensions to pack Ti/Hi correctly — so if the L/W/H fields are still at
    /// their untouched default (0), fill them from the prefab's own mesh bounds (ground-based on Y,
    /// centered on X/Z — the same convention CaseGeneratorTool.BuildBoxMesh writes, so size.x/y/z map
    /// directly to width/height/length). Never overwrites a value the user already typed.</summary>
    private void AutoFillDimensionsFromPrefab(GameObject prefab)
    {
        if (_lengthField.value > 0f || _widthField.value > 0f || _heightField.value > 0f) return;
        if (prefab == null) return;

        var size = GetCombinedLocalBounds(prefab).size; // meters
        _widthField.value = RoundToTenth(size.x * MetersToInches);
        _heightField.value = RoundToTenth(size.y * MetersToInches);
        _lengthField.value = RoundToTenth(size.z * MetersToInches);
        _widthMetric.text = ToMetersLabel(size.x);
        _heightMetric.text = ToMetersLabel(size.y);
        _lengthMetric.text = ToMetersLabel(size.z);
    }

    /// <summary>Same mesh-bounds convention as <see cref="AutoFillDimensionsFromPrefab"/>, but returns
    /// the raw size instead of writing it into the fields — used as the "1x scale" reference point for
    /// <see cref="ApplyDimensionScaleToPreview"/> so an existing prefab's rendered size can be kept in
    /// sync with the L/W/H fields even after they've been edited away from the prefab's native size.</summary>
    private Vector3 GetMeshNativeSizeMeters(GameObject prefab)
    {
        var size = prefab != null ? GetCombinedLocalBounds(prefab).size : Vector3.one;
        return new Vector3(Mathf.Max(size.x, 0.001f), Mathf.Max(size.y, 0.001f), Mathf.Max(size.z, 0.001f));
    }

    /// <summary>The prefab's full visual extent, in the ROOT's own local space, combining EVERY mesh
    /// under it (box + Tape + FlapSeam + Label.* on a case prefab) rather than whichever one happens to
    /// be first under <c>GetComponentInChildren&lt;MeshFilter&gt;</c> — a case prefab has several small
    /// decorative sub-meshes alongside the actual box, and picking the wrong one silently measured the
    /// case's "native size" (and its pivot-to-bottom offset) from a label or tape strip instead of the
    /// box itself, which is what was producing wrong scale factors and wrong vertical seating. Each
    /// mesh's own local bounds are transformed into the root's local frame before combining, so a part
    /// offset or rotated within the prefab doesn't get measured as if it sat at the root's origin.</summary>
    private static Bounds GetCombinedLocalBounds(GameObject root)
    {
        var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        Bounds? combined = null;
        Matrix4x4 rootWorldToLocal = root.transform.worldToLocalMatrix;
        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;
            Matrix4x4 relative = rootWorldToLocal * mf.transform.localToWorldMatrix;
            Bounds mb = mf.sharedMesh.bounds;
            Vector3 c = mb.center, e = mb.extents;
            for (int dx = -1; dx <= 1; dx += 2)
                for (int dy = -1; dy <= 1; dy += 2)
                    for (int dz = -1; dz <= 1; dz += 2)
                    {
                        Vector3 corner = relative.MultiplyPoint3x4(c + Vector3.Scale(e, new Vector3(dx, dy, dz)));
                        if (combined == null) combined = new Bounds(corner, Vector3.zero);
                        else { var b = combined.Value; b.Encapsulate(corner); combined = b; }
                    }
        }
        return combined ?? new Bounds(Vector3.zero, Vector3.one);
    }

    /// <summary>Runs every time a dimension field changes (or a case is (re)selected/loaded) so the
    /// preview — single case and, if already generated, the full case stack — always physically matches
    /// the current L/W/H fields rather than just whatever the source prefab's own mesh happens to be.
    /// No-op for custom cases, since <see cref="RebuildCustomCasePreview"/> already regenerates that
    /// mesh at the exact entered size. Never touches the source prefab ASSET itself — only the live
    /// instances — so this can't corrupt a case prefab shared by other SKUs.</summary>
    private void ApplyDimensionScaleToPreview()
    {
        if (_customCaseToggle.value) return;

        float wIn = _widthField.value, hIn = _heightField.value, lIn = _lengthField.value;
        if (wIn <= 0f || hIn <= 0f || lIn <= 0f) return;

        var desired = new Vector3(wIn * InchesToMeters, hIn * InchesToMeters, lIn * InchesToMeters);
        var scale = new Vector3(
            desired.x / _existingPrefabNativeSizeMeters.x,
            desired.y / _existingPrefabNativeSizeMeters.y,
            desired.z / _existingPrefabNativeSizeMeters.z);

        // How far below a case's transform origin its visual base actually sits, in the case's own
        // UNSCALED local space — 0 if the pivot is really at the bottom (the normal case). Measured
        // once from the PREFAB (via the same combined-mesh bounds as the scale factor above), not from
        // a live scaled instance — every case on the pallet is a clone of the same prefab, so one
        // measurement covers all of them, and reading it from the prefab avoids re-deriving it from
        // whatever an instance's current (already-scaled) transform happens to report.
        float bottomOffsetUnscaled = _caseTemplate != null ? GetCombinedLocalBounds(_caseTemplate).min.y : 0f;

        if (_liveCaseSingle != null)
        {
            _liveCaseSingle.transform.localScale = scale;
            var singlePos = _liveCaseSingle.transform.localPosition;
            singlePos.y = PalletDeckHeight + PreviewLayerGap - bottomOffsetUnscaled * scale.y;
            _liveCaseSingle.transform.localPosition = singlePos;
        }

        if (_previewPalletBuilder != null)
        {
            var loadObj = _previewPalletBuilder.transform.Find("PalletLoad");
            if (loadObj != null)
            {
                // Build() only ever computes each case's Y position ONCE, using whatever case height
                // was current at that moment. A later dimension-field edit only got as far as rescaling
                // the mesh here — the Y positions were never re-derived from the NEW height, so raising
                // the height crammed layers into each other (still spaced for the old, shorter case) and
                // lowering it left a gap (still spaced for the old, taller case). Recompute every case's
                // Y from its layer index instead of leaving Build()'s stale value in place.
                //
                // Every layer sits PreviewLayerGap above whatever is beneath it — deck for layer 0, the
                // previous layer's top for every layer after — so layer i's base = deckY + gap*(i+1) +
                // caseHeight*i, minus the case prefab's own pivot-to-bottom offset (see above) so the
                // RENDERED base lands there, not just the transform origin.
                int ti = Mathf.Max(1, _previewPalletBuilder.manualTi);
                float deckY = _previewPalletBuilder.palletDimensions.y;
                float bottomOffset = bottomOffsetUnscaled * scale.y;
                int i = 0;
                foreach (Transform caseTransform in loadObj)
                {
                    caseTransform.localScale = scale;
                    int layerIndex = i / ti;
                    var pos = caseTransform.localPosition;
                    pos.y = deckY + PreviewLayerGap * (layerIndex + 1) + desired.y * layerIndex - bottomOffset;
                    caseTransform.localPosition = pos;
                    i++;
                }
            }
        }
    }

    private void OnDimensionsChanged()
    {
        if (_customCaseToggle.value) RebuildCustomCasePreview();
        else ApplyDimensionScaleToPreview();

        SaveDimensionsToEditingSku();
    }

    /// <summary>The SKU's ScriptableObject is the source of truth for dimensions, not just whatever's
    /// sitting in the form — so while editing an existing item, every dimension edit is written straight
    /// through to the SO immediately rather than waiting for Submit. No-op for a brand-new (not yet
    /// created) item, since there's no SkuData asset to write into until Submit makes one.</summary>
    private void SaveDimensionsToEditingSku()
    {
#if UNITY_EDITOR
        if (_editingSku == null) return;
        float w = _widthField.value, h = _heightField.value, l = _lengthField.value;
        if (w <= 0f || h <= 0f || l <= 0f) return; // don't persist a mid-edit invalid value

        var so = new SerializedObject(_editingSku);
        so.FindProperty("_caseLength").floatValue = l * InchesToMeters;
        so.FindProperty("_caseWidth").floatValue = w * InchesToMeters;
        so.FindProperty("_caseHeight").floatValue = h * InchesToMeters;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(_editingSku);
#endif
    }

    // ── Custom case (procedural, in-memory) ──────────────────────────────────

    private void RebuildCustomCasePreview()
    {
        if (_iconButtons.Count == 0) BuildIconPicker();

        float w = _widthField.value * InchesToMeters, h = _heightField.value * InchesToMeters, l = _lengthField.value * InchesToMeters;
        if (w <= 0f || h <= 0f || l <= 0f)
        {
            _previewStatusLabel.text = "Enter case Length/Width/Height (inches) to build a custom case.";
            return;
        }

        if (_liveCustomCaseTemplate != null) UnityEngine.Object.Destroy(_liveCustomCaseTemplate);
        _liveCustomCaseTemplate = BuildCaseVisualInMemory(w, h, l, _boxColor, _tapeColor, _selectedIcon);
        _liveCustomCaseTemplate.SetActive(false);
        _caseTemplate = _liveCustomCaseTemplate;

        RefreshCaseReadyState();
        ShowSingleCasePreview();
    }

    private GameObject _liveCustomCaseTemplate;

    private void BuildIconPicker()
    {
        _iconPickerRow.Clear();
        _iconButtons.Clear();
        _iconButtonSprites.Clear();

        var noneBtn = new Button(() => { _selectedIcon = null; HighlightSelectedIcon(); if (_customCaseToggle.value) RebuildCustomCasePreview(); }) { text = "None" };
        StyleIconButton(noneBtn);
        _iconPickerRow.Add(noneBtn);
        _iconButtons.Add(noneBtn);

#if UNITY_EDITOR
        var seen = new HashSet<Sprite>();
        var guids = AssetDatabase.FindAssets("t:SkuData");
        foreach (var guid in guids)
        {
            var sku = AssetDatabase.LoadAssetAtPath<SkuData>(AssetDatabase.GUIDToAssetPath(guid));
            if (sku?.Icon == null || !seen.Add(sku.Icon)) continue;

            var sprite = sku.Icon;
            var btn = new Button(() => { _selectedIcon = sprite; HighlightSelectedIcon(); if (_customCaseToggle.value) RebuildCustomCasePreview(); });
            StyleIconButton(btn);
            btn.style.backgroundImage = new StyleBackground(sprite);
            _iconPickerRow.Add(btn);
            _iconButtons.Add(btn);
            _iconButtonSprites.Add(sprite);
        }
#endif
        HighlightSelectedIcon();
    }

    private void StyleIconButton(Button b)
    {
        b.style.width = 44; b.style.height = 44;
        b.style.marginRight = 4; b.style.marginBottom = 4;
        b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 2;
        b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = new StyleColor(ColSectionEdge);
        b.style.backgroundColor = new StyleColor(ColSectionBg);
        b.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
        b.style.fontSize = 9;
        b.style.color = new StyleColor(ColSubtleText);
    }

    private void HighlightSelectedIcon()
    {
        for (int i = 0; i < _iconButtons.Count; i++)
        {
            // Index 0 is always "None" (sprite == null); every later index lines up with
            // _iconButtonSprites, populated alongside _iconButtons in BuildIconPicker.
            Sprite thisButtonSprite = i == 0 ? null : (i - 1 < _iconButtonSprites.Count ? _iconButtonSprites[i - 1] : null);
            bool selected = thisButtonSprite == _selectedIcon;
            _iconButtons[i].style.borderTopColor = _iconButtons[i].style.borderBottomColor =
                _iconButtons[i].style.borderLeftColor = _iconButtons[i].style.borderRightColor =
                new StyleColor(selected ? ColOrangeEdge : ColSectionEdge);
        }
    }

    private readonly List<Sprite> _iconButtonSprites = new();

    private void RefreshCaseReadyState()
    {
        bool ready = _caseTemplate != null;
        _section2.SetEnabled(ready);
    }

    // ── Preview rig (real 3D objects rendered into a RenderTexture) ─────────

    private void EnsurePreviewRig()
    {
        if (_rigRoot != null) return;

        _rigRoot = new GameObject("[ItemCreatorPreviewRig]");
        _rigRoot.transform.position = new Vector3(0f, 400f, 0f);

        _pivot = new GameObject("Pivot").transform;
        _pivot.SetParent(_rigRoot.transform, false);

        var camGO = new GameObject("PreviewCamera");
        camGO.transform.SetParent(_rigRoot.transform, false);
        camGO.transform.localPosition = new Vector3(0f, 1.3f, -3.4f); // pulled back/up from the original -2.6f so the whole pallet fits in frame
        camGO.transform.localRotation = Quaternion.Euler(14f, 0f, 0f); // slight downward tilt onto the pallet at the pivot's origin
        _previewCamera = camGO.AddComponent<Camera>();
        _previewCamera.clearFlags = CameraClearFlags.SolidColor;
        _previewCamera.backgroundColor = new Color(0.04f, 0.05f, 0.07f, 1f); // near-black void outside the backdrop wall, so the wall reads as a distinct lit panel
        _previewCamera.fieldOfView = 36f;
        _previewCamera.nearClipPlane = 0.1f;
        _previewCamera.farClipPlane = 20f;
        _previewCamera.allowMSAA = true;

        // Blocky/aliased edges are the RT's own MSAA sample count defaulting to 1 — bump it, and also
        // add FXAA on top via the URP camera data so edges stay smooth even where MSAA alone misses
        // (shader-based edges, the RenderTexture's blit into the Image control, etc.).
        _previewRT = new RenderTexture(480, 360, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        _previewRT.Create();
        _previewCamera.targetTexture = _previewRT;
        _previewImage.image = _previewRT;

        var camData = _previewCamera.GetUniversalAdditionalCameraData();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        // Blurry image = the scene's global Depth of Field (and any other Volume override — bloom, color
        // grading, etc.) riding along now that renderPostProcessing is on. FXAA itself is a fixed camera
        // setting, not a Volume component, so it isn't affected by this — an empty volume mask means no
        // Volume in the scene can ever match this camera, which keeps FXAA crisp while dropping DOF/bloom/
        // grading entirely for the preview.
        camData.volumeLayerMask = 0;

        // Key light: back to Directional (see the "Preview quality override" field comment for why —
        // additional-light shadows are unsupported project-wide, so only a Directional light standing in
        // as the MAIN light, via RenderSettings.sun in Show()/Hide(), can ever cast a real shadow here).
        var lightGO = new GameObject("PreviewLight");
        lightGO.transform.SetParent(_rigRoot.transform, false);
        lightGO.transform.localPosition = new Vector3(1.2f, 2.4f, -1.5f);
        lightGO.transform.LookAt(_rigRoot.transform.position + Vector3.up * 0.6f);
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.66f; // halved per earlier request, then +20%
        light.color = new Color(0.95f, 0.94f, 0.90f, 1f);
        light.shadows = LightShadows.Soft; // needed for the backdrop below to actually catch a shadow
        light.shadowResolution = LightShadowResolution.High;
        var lightData = lightGO.GetComponent<UniversalAdditionalLightData>();
        if (lightData == null) lightData = lightGO.AddComponent<UniversalAdditionalLightData>();
        lightData.softShadowQuality = SoftShadowQuality.High; // soft-edged shadow on the backdrop instead of a hard-edged one
        _previewKeyLight = light;

        BuildBackdrop();

        var chepPrefab = Resources.Load<GameObject>("ChepEmpty");
        if (chepPrefab != null)
        {
            _chepInstance = UnityEngine.Object.Instantiate(chepPrefab, _pivot);
            _chepInstance.transform.localPosition = Vector3.zero;
            _chepInstance.transform.localRotation = Quaternion.identity;
            StripPlacementComponents(_chepInstance);
        }
    }

    /// <summary>Back wall behind the pallet, parented to the RIG (not the pivot) so it never spins or
    /// tilts with the pallet — just a static backdrop that gives the rotating pallet a shadow to cast,
    /// for some depth instead of it floating in flat black. No floor plane, so nothing sits between the
    /// camera and the backdrop.</summary>
    private void BuildBackdrop()
    {
        var backdropColor = new Color(0.87f, 0.80f, 0.68f, 1f); // light tan

        // A thin Cube instead of a Quad — a Quad has exactly one visible face by default (its opposite
        // side is a backface, culled or lit wrong depending on the material's _Cull setting), which was
        // a second possible reason this wall could render as empty/black regardless of color or light.
        // A Cube has no "wrong side" to get backwards, so this removes that guesswork entirely.
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "PreviewWall";
        UnityEngine.Object.Destroy(wall.GetComponent<Collider>());
        wall.transform.SetParent(_rigRoot.transform, false);
        wall.transform.localPosition = new Vector3(0f, 0f, 1.6f); // behind the pallet, relative to the camera at z=-3.4
        // Sized to fully fill the camera's frame at that distance (camera-to-wall = 5.0, FOV 36°, 4:3
        // RT aspect), with margin, and centered/tall enough to cover the frustum's downward shift from
        // the camera's 14° tilt — a shorter wall left a visible void seam near the bottom of the frame.
        // Z-depth is thin but non-zero (a Cube, unlike a Quad, needs real depth to have any thickness).
        wall.transform.localScale = new Vector3(5.5f, 6.0f, 0.1f);
        // Built from the pipeline's own default material (the same one CreatePrimitive assigns
        // automatically) rather than `new Material(Shader.Find(...))` — a raw shader lookup skips the
        // keyword/render-state setup the Inspector's ShaderGUI normally does for a URP Lit material,
        // which is what was leaving this wall rendering as invisible/black regardless of color or light.
        var wallMat = new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial) { color = backdropColor };
        // A touch of smoothness so the key/uplights put a soft specular highlight on the wall instead of
        // it reading as flat diffuse — kept low (and non-metallic) so it doesn't look like polished metal.
        if (wallMat.HasProperty("_Smoothness")) wallMat.SetFloat("_Smoothness", 0.4f);
        if (wallMat.HasProperty("_Metallic")) wallMat.SetFloat("_Metallic", 0.05f);
        // Belt-and-suspenders: after three rounds of "still black" despite a correctly-colored, correctly
        // shaped, correctly shadered wall, something about this rig's lighting/post-processing was crushing
        // it to black before it ever reached the screen. Giving the wall its own emissive glow makes it
        // visible independent of that — but kept low (a floor, not the main brightness) so the key light's
        // actual cast shadow still reads as real contrast instead of getting washed out by a flat glow.
        if (wallMat.HasProperty("_EmissionColor"))
        {
            wallMat.EnableKeyword("_EMISSION");
            wallMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            wallMat.SetColor("_EmissionColor", backdropColor * 0.2f);
        }
        var wallRenderer = wall.GetComponent<MeshRenderer>();
        wallRenderer.sharedMaterial = wallMat;
        wallRenderer.receiveShadows = true;
        wallRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Debug.Log($"[ItemCreatorPanel] PreviewWall built: shader={wallMat.shader?.name ?? "NULL"}, " +
            $"worldPos={wall.transform.position}, scale={wall.transform.lossyScale}, " +
            $"rendererEnabled={wallRenderer.enabled}, layer={wall.layer}");
    }

    private void ClearPreview()
    {
        if (_liveCaseSingle != null) { UnityEngine.Object.Destroy(_liveCaseSingle); _liveCaseSingle = null; }
        if (_previewPalletBuilder != null)
        {
            var loadObj = _previewPalletBuilder.transform.Find("PalletLoad");
            if (loadObj != null) UnityEngine.Object.Destroy(loadObj.gameObject);
        }
    }

    private void ShowSingleCasePreview()
    {
        if (_pivot == null || _caseTemplate == null) return;
        ClearPreview();

        _liveCaseSingle = UnityEngine.Object.Instantiate(_caseTemplate, _pivot);
        _liveCaseSingle.SetActive(true);
        StripPlacementComponents(_liveCaseSingle);
        _liveCaseSingle.transform.localPosition = new Vector3(0f, PalletDeckHeight, 0f);
        _liveCaseSingle.transform.localRotation = CasePalletYawCorrection;

        _previewStatusLabel.text = "Case ready. Set Ti/Hi and click Generate Preview to build the full pallet.";
    }

    private void OnGeneratePreviewClicked()
    {
        if (_pivot == null || _caseTemplate == null)
        {
            _previewStatusLabel.text = "Choose or create a case first.";
            return;
        }

        int ti = Mathf.Max(1, _tiField.value);
        int hi = Mathf.Max(1, _hiField.value);
        float w = _widthField.value * InchesToMeters, h = _heightField.value * InchesToMeters, l = _lengthField.value * InchesToMeters;
        if (w <= 0f || h <= 0f || l <= 0f)
        {
            _previewStatusLabel.text = "Case dimensions must be greater than zero.";
            return;
        }

        if (_liveCaseSingle != null) { UnityEngine.Object.Destroy(_liveCaseSingle); _liveCaseSingle = null; }

        if (_previewPalletBuilder == null)
        {
            var pbGO = new GameObject("PreviewPalletBuilder");
            pbGO.transform.SetParent(_pivot, false);
            pbGO.transform.localPosition = Vector3.zero;
            _previewPalletBuilder = pbGO.AddComponent<PalletBuilder>();
        }
        _previewPalletBuilder.transform.localRotation = Quaternion.identity; // the builder itself stays unrotated — see CasePalletYawCorrection below

        _previewPalletBuilder.casePrefab = _caseTemplate;
        _previewPalletBuilder.linkedSku = null;
        _previewPalletBuilder.caseDimensions = new Vector3(w, h, l);
        _previewPalletBuilder.useTiHiOverride = true;
        _previewPalletBuilder.manualTi = ti;
        _previewPalletBuilder.manualHi = hi;
        _previewPalletBuilder.crookedCase = 0f; // no random jitter in the preview — see AlignGeneratedCasesToPallet's old approach for why zeroing rotation after the fact doesn't work
        _previewPalletBuilder.verticalGap = PreviewLayerGap; // real gameplay pallets stack flush (0) on purpose — this gap is preview-only, so layers read clearly
        _previewPalletBuilder.Build(deductMoney: false);

        // The ChepEmpty mesh's real footprint sits 90° from PalletBuilder's assumed X=width/Z=length
        // axes (confirmed by hand in the Inspector — PalletLoad at Y=90 is what lines cases up with the
        // pallet visual). PalletLoad is a fresh GameObject Build() creates every call, so this has to be
        // reapplied after every Build() — nothing else in this file touches PalletLoad's rotation
        // afterward (ApplyDimensionScaleToPreview only touches scale), so this is the only place it can
        // get silently reset back to identity.
        var loadObj = _previewPalletBuilder.transform.Find("PalletLoad");
        if (loadObj != null) loadObj.localRotation = CasePalletYawCorrection;

        ApplyDimensionScaleToPreview(); // freshly-instantiated cases start at the prefab's native scale — bring them in line with the current fields immediately

        if (_previewPalletBuilder.TotalCases == 0)
        {
            _previewStatusLabel.text = "That case doesn't fit the pallet at all — check dimensions.";
        }
        else
        {
            _previewStatusLabel.text = $"Preview built: {_previewPalletBuilder.TotalCases} case(s).";
        }
    }

    private static void StripPlacementComponents(GameObject go)
    {
        foreach (var po in go.GetComponentsInChildren<PlacedObject>(true)) { po.enabled = false; UnityEngine.Object.Destroy(po); }
        foreach (var bd in go.GetComponentsInChildren<BuildingData>(true)) UnityEngine.Object.Destroy(bd);
        foreach (var bh in go.GetComponentsInChildren<BuildingHighlighter>(true)) UnityEngine.Object.Destroy(bh);
    }

    // ── Rotation: auto-spin + drag ────────────────────────────────────────────

    private void StartTicking()
    {
        _tick?.Pause();
        _tick = _modal.schedule.Execute(Tick).Every(16);
    }

    private void Tick(TimerState _)
    {
        if (!_visible || _pivot == null) return;
        const float dt = 0.016f;

        if (!_dragging)
        {
            _yawDeg += SpinDegPerSec * dt;
            // Pitch always eases back to its original (0) orientation once released — slow and subtle,
            // not a snap. Zeroed out once it's close enough that the lerp would otherwise crawl forever.
            _pitchDeg = Mathf.Lerp(_pitchDeg, 0f, dt * PitchReturnLerpPerSec);
            if (Mathf.Abs(_pitchDeg) < 0.05f) _pitchDeg = 0f;
        }

        ApplyPivotRotation();
    }

    private void ApplyPivotRotation()
    {
        _pivot.localRotation = Quaternion.Euler(_pitchDeg, _yawDeg, 0f);
    }

    private void OnPreviewPointerDown(PointerDownEvent evt)
    {
        _dragging = true;
        _lastDragX = evt.position.x;
        _lastDragY = evt.position.y;
        _previewImage.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnPreviewPointerMove(PointerMoveEvent evt)
    {
        if (!_dragging || _pivot == null) return;
        float dx = evt.position.x - _lastDragX;
        float dy = evt.position.y - _lastDragY;
        _lastDragX = evt.position.x;
        _lastDragY = evt.position.y;

        _yawDeg -= dx * DragDegPerPixel;
        _pitchDeg = Mathf.Clamp(_pitchDeg - dy * PitchDragDegPerPixel * PitchDragDirection, -MaxPitchDeg, MaxPitchDeg);
        ApplyPivotRotation();
        evt.StopPropagation();
    }

    private void OnPreviewPointerUp(PointerUpEvent evt)
    {
        if (!_dragging) return;
        _dragging = false;
        if (_previewImage.HasPointerCapture(evt.pointerId)) _previewImage.ReleasePointer(evt.pointerId);
        evt.StopPropagation();
        // Pitch spring-back and yaw auto-spin both resume automatically via the next Tick().
    }

    // ── Submit / Delete ───────────────────────────────────────────────────────

    private void OnSubmitClicked()
    {
        string description = _descriptionField.value?.Trim();
        if (string.IsNullOrEmpty(description)) { UIToast.Show("Enter an item description first."); return; }
        if (_lengthField.value <= 0f || _widthField.value <= 0f || _heightField.value <= 0f)
        { UIToast.Show("Case dimensions must be greater than zero."); return; }
        if (_weightField.value <= 0f) { UIToast.Show("Enter a case weight."); return; }
        if (_caseTemplate == null) { UIToast.Show("Choose or create a case prefab first."); return; }

#if UNITY_EDITOR
        GameObject finalPrefab;
        if (_customCaseToggle.value)
        {
            finalPrefab = BuildAndSaveCasePrefab(_currentItemNumber, description,
                _widthField.value * InchesToMeters, _heightField.value * InchesToMeters, _lengthField.value * InchesToMeters,
                _boxColor, _tapeColor, _selectedIcon);
            if (finalPrefab == null) { UIToast.Show("Failed to generate the case prefab — check the Console."); return; }
        }
        else
        {
            finalPrefab = _caseTemplate;
        }

        SkuData sku = _editingSku != null ? _editingSku : ScriptableObject.CreateInstance<SkuData>();
        bool isNew = _editingSku == null;

        var so = new SerializedObject(sku);
        if (isNew) so.FindProperty("_itemNumber").intValue = _currentItemNumber;
        so.FindProperty("_itemDescription").stringValue = description;
        so.FindProperty("_caseLength").floatValue = _lengthField.value * InchesToMeters;
        so.FindProperty("_caseWidth").floatValue = _widthField.value * InchesToMeters;
        so.FindProperty("_caseHeight").floatValue = _heightField.value * InchesToMeters;
        so.FindProperty("_csWeight").floatValue = _weightField.value;
        so.FindProperty("_storageArea").enumValueIndex = (int)(PalletData.AreaCategory)_storageAreaField.value;
        so.FindProperty("_buyValue").floatValue = _buyValueField.value;
        so.FindProperty("_sellValue").floatValue = _sellValueField.value;
        so.FindProperty("_ti").intValue = Mathf.Max(1, _tiField.value);
        so.FindProperty("_hi").intValue = Mathf.Max(1, _hiField.value);
        so.FindProperty("_prefab").objectReferenceValue = finalPrefab;
        so.FindProperty("_icon").objectReferenceValue = _selectedIcon;
        so.FindProperty("_shelfLifeDays").intValue = _shelfLifeField.value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sku);

        if (isNew)
        {
            string path = $"Assets/_Project/Resources/Inventory/SKUs/SKU_{_currentItemNumber}.asset";
            AssetDatabase.CreateAsset(sku, path);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        UIToast.Show($"Saved {description} as SKU_{_currentItemNumber}. Takes effect next Play session (SKUs load once at startup).");

        if (isNew)
        {
            _editingSku = sku;
            _isEditMode = true;
            _deleteButton.SetEnabled(true);
        }
#else
        UIToast.Show("Submitting to the item database is Editor-only.");
#endif
    }

    private void OnDeleteClicked()
    {
        if (_editingSku == null) return;
        string description = _editingSku.ItemDescription;
        int itemNumber = _editingSku.ItemNumber;

        ConfirmationModal.Show($"Delete item \"{description}\" (SKU {itemNumber})? This cannot be undone.",
            onYes: () =>
            {
#if UNITY_EDITOR
                string path = AssetDatabase.GetAssetPath(_editingSku);
                if (!string.IsNullOrEmpty(path)) AssetDatabase.DeleteAsset(path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                UIToast.Show($"Deleted SKU {itemNumber}.");
#else
                UIToast.Show("Deleting items is Editor-only.");
#endif
                EnterEditItemMode();
                ClearForm();
            });
    }

#if UNITY_EDITOR
    private static int GenerateNextItemNumber()
    {
        int max = 100000;
        foreach (var guid in AssetDatabase.FindAssets("t:SkuData"))
        {
            var sku = AssetDatabase.LoadAssetAtPath<SkuData>(AssetDatabase.GUIDToAssetPath(guid));
            if (sku != null && sku.ItemNumber > max) max = sku.ItemNumber;
        }
        return max + 1;
    }
#else
    private static int GenerateNextItemNumber()
    {
        // Authoring (item numbering, prefab/asset creation) is Editor-only — see class doc comment.
        int max = 100000;
        foreach (var sku in Resources.LoadAll<SkuData>("Inventory/SKUs"))
            if (sku != null && sku.ItemNumber > max) max = sku.ItemNumber;
        return max + 1;
    }
#endif

#if UNITY_EDITOR
    private static string SanitizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Item";
        foreach (char ch in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(ch.ToString(), "_");
        return name.Trim().Replace(" ", "");
    }

    private static void EnsureFolder(string path)
    {
        path = path.Replace("\\", "/").TrimEnd('/');
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/") ?? "Assets";
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    /// <summary>Builds and persists a case prefab (mesh + materials + tape + optional icon label) to
    /// disk, mirroring CaseGeneratorTool's on-disk conventions so the result is a normal, reusable case
    /// prefab like every hand-generated one — but using the player's chosen colors/icon instead of a
    /// random roll.</summary>
    private static GameObject BuildAndSaveCasePrefab(int itemNumber, string description, float w, float h, float l,
        Color boxColor, Color tapeColor, Sprite icon)
    {
        const string prefabDir = "Assets/_Project/Prefabs/Inventory/Cases";
        const string meshDir = "Assets/_Project/Prefabs/Inventory/Cases/Meshes";
        const string matDir = "Assets/_Project/Prefabs/Inventory/Cases/Materials";
        EnsureFolder(prefabDir); EnsureFolder(meshDir); EnsureFolder(matDir);

        string safeName = $"Cs_{itemNumber}_{SanitizeName(description)}";
        string meshPath = $"{meshDir}/{safeName}_Mesh.asset";
        string matPath = $"{matDir}/{safeName}_Mat.mat";
        string tapeMatPath = $"{matDir}/{safeName}_Tape.mat";
        string labelMatPath = $"{matDir}/{safeName}_Label.mat";
        string prefabPath = $"{prefabDir}/{safeName}.prefab";

        foreach (var p in new[] { meshPath, matPath, tapeMatPath, labelMatPath, prefabPath })
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p) != null) AssetDatabase.DeleteAsset(p);

        Mesh mesh = BuildBoxMesh(w, h, l);
        mesh.name = safeName + "_Mesh";
        AssetDatabase.CreateAsset(mesh, meshPath);

        // Built from the pipeline's own default material rather than `new Material(Shader.Find(...))` —
        // a raw shader lookup skips the keyword/render-state setup the Inspector's ShaderGUI normally
        // does for a URP Lit material, which left the item-creator preview's backdrop wall invisible
        // (same construction pattern, same bug) until it was switched to this.
        Material mat = new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial) { name = safeName + "_Mat" };
        mat.color = boxColor;
        mat.SetFloat("_Smoothness", 0.12f);
        AssetDatabase.CreateAsset(mat, matPath);

        GameObject root = BuildCaseGameObjectCommon(safeName, mesh, mat, w, h, l, tapeColor, icon,
            out Material tapeMat, out Material labelMat);

        AssetDatabase.CreateAsset(tapeMat, tapeMatPath);
        if (labelMat != null) AssetDatabase.CreateAsset(labelMat, labelMatPath);

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool ok);
        UnityEngine.Object.DestroyImmediate(root);
        if (!ok) return null;

        // Force a synchronous import before handing back the reference — see CaseGeneratorTool's
        // documented note: a later AssetDatabase.Refresh() elsewhere can renumber the in-memory root
        // fileID SaveAsPrefabAsset returns, silently breaking the SkuData->prefab link.
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }
#endif

    /// <summary>Builds the same case visual as <see cref="BuildAndSaveCasePrefab"/>, entirely in memory
    /// (no AssetDatabase calls) — safe to call every time a field changes for the live preview.</summary>
    private static GameObject BuildCaseVisualInMemory(float w, float h, float l, Color boxColor, Color tapeColor, Sprite icon)
    {
        Mesh mesh = BuildBoxMesh(w, h, l);
        Material mat = new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial);
        mat.color = boxColor;
        mat.SetFloat("_Smoothness", 0.12f);

        return BuildCaseGameObjectCommon("CustomCasePreview", mesh, mat, w, h, l, tapeColor, icon, out _, out _);
    }

    /// <summary>Shared box+tape+optional-icon-label construction, used by both the in-memory preview
    /// and the on-disk asset generator. Clones Unity's built-in cube mesh and rescales its VERTICES
    /// directly (ground-based on Y, centered on X/Z) — see CaseGeneratorTool.BuildBoxMesh for why:
    /// PalletBuilder's case placement math assumes a case's own transform origin sits at the bottom of
    /// the case, and mesh bounds (not a scaled transform) are what anything reading prefab size sees.</summary>
    private static GameObject BuildCaseGameObjectCommon(string name, Mesh mesh, Material mat, float w, float h, float l,
        Color tapeColor, Sprite icon, out Material tapeMat, out Material labelMat)
    {
        GameObject root = new GameObject(name);
        var mf = root.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = root.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;

        const float tapeThickness = 0.0015f;
        const float tapeOffset = tapeThickness / 2f + 0.0002f;

        tapeMat = new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial) { name = name + "_Tape" };
        tapeMat.color = tapeColor;

        GameObject mainTape = GameObject.CreatePrimitive(PrimitiveType.Cube);
        UnityEngine.Object.DestroyImmediate(mainTape.GetComponent<Collider>());
        mainTape.name = "Tape_Main";
        mainTape.transform.SetParent(root.transform, false);
        mainTape.transform.localScale = new Vector3(Mathf.Max(0.02f, w * 0.16f), tapeThickness, l * 1.001f);
        mainTape.transform.localPosition = new Vector3(0f, h + tapeOffset, 0f);
        mainTape.GetComponent<MeshRenderer>().sharedMaterial = tapeMat;

        labelMat = null;
        if (icon != null)
        {
            labelMat = new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial) { name = name + "_Label" };
            labelMat.mainTexture = icon.texture;
            labelMat.color = Color.white;

            const float labelPopOut = 0.06f;
            float labelW = Mathf.Min(l, w) * 0.5f;
            float labelH = h * 0.5f;

            GameObject label = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(label.GetComponent<Collider>());
            label.name = "Label_Front";
            label.transform.SetParent(root.transform, false);
            label.transform.localScale = new Vector3(labelW, labelH, 1f);
            label.transform.localPosition = new Vector3(0f, h / 2f, l / 2f + labelPopOut);
            label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            label.GetComponent<MeshRenderer>().sharedMaterial = labelMat;
        }

        return root;
    }

    private static Mesh BuildBoxMesh(float w, float h, float l)
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh source = temp.GetComponent<MeshFilter>().sharedMesh;
        Mesh newMesh = UnityEngine.Object.Instantiate(source);

        Vector3[] verts = newMesh.vertices;
        for (int i = 0; i < verts.Length; i++)
            verts[i] = new Vector3(verts[i].x * w, verts[i].y * h + h / 2f, verts[i].z * l);
        newMesh.vertices = verts;
        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();
        newMesh.RecalculateTangents();

        UnityEngine.Object.DestroyImmediate(temp);
        return newMesh;
    }

    // ── Small UI helpers ─────────────────────────────────────────────────────

    private VisualElement MakeSectionContainer(string title, out VisualElement content)
    {
        var section = new VisualElement();
        section.style.backgroundColor = new StyleColor(ColSectionBg);
        section.style.borderTopWidth = section.style.borderBottomWidth =
            section.style.borderLeftWidth = section.style.borderRightWidth = 2;
        section.style.borderTopColor = section.style.borderBottomColor =
            section.style.borderLeftColor = section.style.borderRightColor = new StyleColor(ColSectionEdge);
        section.style.borderTopLeftRadius = section.style.borderTopRightRadius =
            section.style.borderBottomLeftRadius = section.style.borderBottomRightRadius = 10;
        section.style.paddingLeft = 12; section.style.paddingRight = 12;
        section.style.paddingTop = 10; section.style.paddingBottom = 10;

        var header = MakeText(title, 17, ColTitleText, bold: true); // 13 + 30%
        header.style.marginBottom = 8;
        header.style.whiteSpace = WhiteSpace.Normal;
        section.Add(header);

        content = new VisualElement();
        section.Add(content);
        return section;
    }

    private TextField MakeLabeledText(string label, string defaultVal, Action<string> onChange)
    {
        var f = new TextField(label) { value = defaultVal };
        StyleLabeled(f);
        f.RegisterValueChangedCallback(evt => onChange(evt.newValue));
        return f;
    }

    private FloatField MakeLabeledFloat(string label, float defaultVal, Action<float> onChange)
    {
        var f = new FloatField(label) { value = defaultVal };
        StyleLabeled(f);
        f.RegisterValueChangedCallback(evt => onChange(evt.newValue));
        return f;
    }

    private void StyleLabeled(VisualElement field)
    {
        ApplyFont(field, size: 17); // 20% larger than the previous unset (~14px) default
        field.style.marginBottom = 6;
        var label = field.Q<Label>();
        if (label != null)
        {
            label.style.color = new StyleColor(ColSubtleText);
            label.style.minWidth = 150;
            label.style.fontSize = 19.5f; // field's own 17px + 15%, independent of the input text's size
        }
    }

    /// <summary>StyleLabeled's 150px label minWidth is sized for full-width single-column rows. Fields
    /// sharing a half-width row (Buy/Sell Value, Ti/Hi) need a narrower label or that floor forces the
    /// field wider than its flex-grow share, overflowing the row's right edge out from under the other
    /// boxes above and under the ScrollView's scrollbar.</summary>
    private void SetCompactLabelWidth(VisualElement field)
    {
        var label = field.Q<Label>();
        if (label == null) return;
        label.style.minWidth = 85;
        label.style.whiteSpace = WhiteSpace.Normal;
    }

    private Button MakeActionButton(string text, Color face, Color edge, Color hover, Action onClick)
    {
        var b = new Button(onClick) { text = text };
        ApplyFont(b, bold: true, size: 13);
        b.style.height = 34;
        b.style.color = new StyleColor(ColOrangeText);
        b.style.backgroundColor = new StyleColor(face);
        b.style.borderTopWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 2;
        b.style.borderBottomWidth = 3;
        b.style.borderTopColor = b.style.borderLeftColor = b.style.borderRightColor = new StyleColor(edge);
        b.style.borderBottomColor = new StyleColor(edge);
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 8;
        b.RegisterCallback<PointerEnterEvent>(_ => b.style.backgroundColor = new StyleColor(hover));
        b.RegisterCallback<PointerLeaveEvent>(_ => b.style.backgroundColor = new StyleColor(face));
        return b;
    }

    private Label MakeText(string text, int size, Color color, bool bold = false)
    {
        var label = new Label(text);
        ApplyFont(label, bold, size);
        label.style.color = new StyleColor(color);
        return label;
    }

    private static Font LilitaFont()
    {
        if (_lilita != null) return _lilita;
#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("LilitaOne-Regular t:Font");
        if (guids.Length > 0)
            _lilita = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guids[0]));
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
}
