using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Inventory;
using UnityEngine;
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
    private const float PalletDeckHeight = 0.165f;

    private static Font _lilita;

    // ── Chrome ───────────────────────────────────────────────────────────────
    private readonly VisualElement _overlay;
    private readonly VisualElement _modal;
    private ResizableWindow _resizer;
    private DraggableWindow _dragger;
    private Button _scaleButton;
    private bool _visible;

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
    private FloatField _lengthField, _widthField, _heightField;
    private Label _lengthInches, _widthInches, _heightInches;
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
    // Flip to -1f if a drag ends up rotating the opposite way from what feels natural once tested live.
    private const float PitchDragDirection = 1f;
    // Measured once per pallet visual (see MeasurePalletYawCorrection) — corrects for the CHEP mesh's
    // real footprint not necessarily matching PalletBuilder's assumed X=width/Z=length orientation, so
    // the generated case layer can't end up rotated 90° from the actual pallet shape (askew, hanging
    // off the corners).
    private float _palletYawCorrectionDeg;
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
        _resizer?.ResetToNormal();
        if (_scaleButton != null) PanelTitleChrome.SyncScaleGlyph(_scaleButton, _resizer);
        UIModalGuard.Push(this);
        bool rigJustCreated = _rigRoot == null;
        EnsurePreviewRig();
        // The rig is built lazily here, but a case may already have been selected earlier (e.g. the
        // default existing-prefab choice made during construction, before any rig existed) — that
        // selection's ShowSingleCasePreview() call was a silent no-op back then, so re-show it now
        // that there's actually a pivot to parent onto.
        if (rigJustCreated && _caseTemplate != null) ShowSingleCasePreview();
        StartTicking();
    }

    public void Hide()
    {
        _visible = false;
        _overlay.style.display = DisplayStyle.None;
        UIModalGuard.Pop(this);
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

        _newItemTab = MakeActionButton("Start New Item", ColBlueBtn, ColBlueEdge, ColBlueHover, EnterNewItemMode);
        _newItemTab.style.width = 180;
        row.Add(_newItemTab);

        _editItemTab = MakeActionButton("Edit Existing Item", ColBlueBtn, ColBlueEdge, ColBlueHover, EnterEditItemMode);
        _editItemTab.style.width = 180;
        _editItemTab.style.marginLeft = 8;
        row.Add(_editItemTab);

        _editDropdown = new DropdownField(new List<string> { "(no items yet)" }, 0);
        _editDropdown.style.flexGrow = 1;
        _editDropdown.style.marginLeft = 12;
        _editDropdown.style.display = DisplayStyle.None;
        _editDropdown.RegisterValueChangedCallback(evt => OnEditDropdownChanged(evt.newValue));
        ApplyFont(_editDropdown);
        row.Add(_editDropdown);

        return row;
    }

    // ── Section 1 ────────────────────────────────────────────────────────────

    private VisualElement BuildSection1()
    {
        var section = MakeSectionContainer("1. Setup or choose your case prefab first!", out var content);

        _itemNumberLabel = MakeText("Item Number: —", 13, ColSubtleText, bold: true);
        _itemNumberLabel.style.marginBottom = 8;
        content.Add(_itemNumberLabel);

        _descriptionField = MakeLabeledText("Item Description", "", v => { });
        content.Add(_descriptionField);

        content.Add(BuildDimensionRow("Case Length (m)", out _lengthField, out _lengthInches));
        content.Add(BuildDimensionRow("Case Width (m)", out _widthField, out _widthInches));
        content.Add(BuildDimensionRow("Case Height (m)", out _heightField, out _heightInches));

        _lengthField.RegisterValueChangedCallback(evt => { _lengthInches.text = ToInchesLabel(evt.newValue); OnDimensionsChanged(); });
        _widthField.RegisterValueChangedCallback(evt => { _widthInches.text = ToInchesLabel(evt.newValue); OnDimensionsChanged(); });
        _heightField.RegisterValueChangedCallback(evt => { _heightInches.text = ToInchesLabel(evt.newValue); OnDimensionsChanged(); });

        _weightField = MakeLabeledFloat("Case Weight (lbs)", 0f, _ => { });
        content.Add(_weightField);

        _storageAreaField = new EnumField("Storage Area", PalletData.AreaCategory.Grocery);
        StyleLabeled(_storageAreaField);
        content.Add(_storageAreaField);

        var priceRow = new VisualElement();
        priceRow.style.flexDirection = FlexDirection.Row;
        _buyValueField = MakeLabeledFloat("Buy Value", 0f, _ => { });
        _buyValueField.style.flexGrow = 1;
        _sellValueField = MakeLabeledFloat("Sell Value", 0f, _ => { });
        _sellValueField.style.flexGrow = 1;
        _sellValueField.style.marginLeft = 8;
        priceRow.Add(_buyValueField);
        priceRow.Add(_sellValueField);
        content.Add(priceRow);

        _shelfLifeField = new IntegerField("Shelf Life Days (-1 = non-perishable)") { value = -1 };
        StyleLabeled(_shelfLifeField);
        content.Add(_shelfLifeField);

        content.Add(BuildDivider());
        content.Add(MakeText("Case Prefab", 13, ColTitleText, bold: true));

        _customCaseToggle = new Toggle("Create a custom case instead") { value = false };
        StyleLabeled(_customCaseToggle);
        _customCaseToggle.RegisterValueChangedCallback(evt => SetCustomCaseMode(evt.newValue));
        content.Add(_customCaseToggle);

        _existingPrefabRow = new VisualElement();
        _existingPrefabDropdown = new DropdownField(new List<string> { "(none found)" }, 0);
        ApplyFont(_existingPrefabDropdown);
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

    private VisualElement BuildDimensionRow(string label, out FloatField field, out Label inches)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;

        field = new FloatField(label) { value = 0f };
        field.style.flexGrow = 1;
        StyleLabeled(field);
        row.Add(field);

        inches = MakeText("0.0\"", 12, ColSubtleText);
        inches.style.width = 60;
        inches.style.marginLeft = 6;
        inches.style.unityTextAlign = TextAnchor.MiddleLeft;
        row.Add(inches);

        return row;
    }

    private static string ToInchesLabel(float meters) => $"{(meters * MetersToInches):0.0}\"";

    private void SetCustomCaseMode(bool custom)
    {
        _existingPrefabRow.style.display = custom ? DisplayStyle.None : DisplayStyle.Flex;
        _customCaseRow.style.display = custom ? DisplayStyle.Flex : DisplayStyle.None;
        if (custom) RebuildCustomCasePreview();
        else OnExistingPrefabChosen(_existingPrefabDropdown?.value);
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
        row.Add(_tiField);

        _hiField = new IntegerField("Hi (layers)") { value = 1 };
        _hiField.style.flexGrow = 1;
        _hiField.style.marginLeft = 8;
        StyleLabeled(_hiField);
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

        var caption = MakeText("PALLET PREVIEW", 14, ColOrangeText, bold: true);
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
        _lengthField.value = sku.CaseLength;
        _widthField.value = sku.CaseWidth;
        _heightField.value = sku.CaseHeight;
        _lengthInches.text = ToInchesLabel(sku.CaseLength);
        _widthInches.text = ToInchesLabel(sku.CaseWidth);
        _heightInches.text = ToInchesLabel(sku.CaseHeight);
        _weightField.value = sku.CaseWeight;
        _storageAreaField.value = sku.StorageArea;
        _buyValueField.value = sku.BuyValue;
        _sellValueField.value = sku.SellValue;
        _shelfLifeField.value = sku.ShelfLifeDays;
        _tiField.value = Mathf.Max(1, sku.Ti);
        _hiField.value = Mathf.Max(1, sku.Hi);

        _customCaseToggle.SetValueWithoutNotify(false);
        SetCustomCaseMode(false);

        PopulateExistingPrefabDropdown(sku.Prefab);
        _caseTemplate = sku.Prefab;
        RefreshCaseReadyState();
        ShowSingleCasePreview();
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
        _lengthInches.text = "0.0\""; _widthInches.text = "0.0\""; _heightInches.text = "0.0\"";
        _weightField.value = 0f;
        _storageAreaField.value = PalletData.AreaCategory.Grocery;
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
            AutoFillDimensionsFromPrefab(go);
            RefreshCaseReadyState();
            ShowSingleCasePreview();
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
        var mf = prefab != null ? prefab.GetComponentInChildren<MeshFilter>() : null;
        if (mf?.sharedMesh == null) return;

        var size = mf.sharedMesh.bounds.size;
        _widthField.value = size.x;
        _heightField.value = size.y;
        _lengthField.value = size.z;
        _widthInches.text = ToInchesLabel(size.x);
        _heightInches.text = ToInchesLabel(size.y);
        _lengthInches.text = ToInchesLabel(size.z);
    }

    private void OnDimensionsChanged()
    {
        if (_customCaseToggle.value) RebuildCustomCasePreview();
    }

    // ── Custom case (procedural, in-memory) ──────────────────────────────────

    private void RebuildCustomCasePreview()
    {
        if (_iconButtons.Count == 0) BuildIconPicker();

        float w = _widthField.value, h = _heightField.value, l = _lengthField.value;
        if (w <= 0f || h <= 0f || l <= 0f)
        {
            _previewStatusLabel.text = "Enter case Length/Width/Height (meters) to build a custom case.";
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
        camGO.transform.localPosition = new Vector3(0f, 1.1f, -2.6f);
        camGO.transform.localRotation = Quaternion.Euler(12f, 0f, 0f); // slight downward tilt onto the pallet at the pivot's origin
        _previewCamera = camGO.AddComponent<Camera>();
        _previewCamera.clearFlags = CameraClearFlags.SolidColor;
        _previewCamera.backgroundColor = new Color(0.15f, 0.19f, 0.24f, 1f); // lightened from the original near-black for more depth/readability
        _previewCamera.fieldOfView = 32f;
        _previewCamera.nearClipPlane = 0.1f;
        _previewCamera.farClipPlane = 20f;

        _previewRT = new RenderTexture(480, 360, 24, RenderTextureFormat.ARGB32);
        _previewRT.Create();
        _previewCamera.targetTexture = _previewRT;
        _previewImage.image = _previewRT;

        var lightGO = new GameObject("PreviewLight");
        lightGO.transform.SetParent(_rigRoot.transform, false);
        lightGO.transform.localPosition = new Vector3(1.2f, 2.4f, -1.5f);
        lightGO.transform.LookAt(_rigRoot.transform.position + Vector3.up * 0.6f);
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.color = new Color(0.95f, 0.94f, 0.90f, 1f);
        light.shadows = LightShadows.Soft; // needed for the backdrop below to actually catch a shadow

        var fillGO = new GameObject("PreviewFill");
        fillGO.transform.SetParent(_rigRoot.transform, false);
        fillGO.transform.localPosition = new Vector3(-1f, 1.2f, -1f);
        var fill = fillGO.AddComponent<Light>();
        fill.type = LightType.Point;
        fill.intensity = 0.6f;
        fill.range = 6f;

        BuildBackdrop();

        var chepPrefab = Resources.Load<GameObject>("ChepEmpty");
        if (chepPrefab != null)
        {
            _chepInstance = UnityEngine.Object.Instantiate(chepPrefab, _pivot);
            _chepInstance.transform.localPosition = Vector3.zero;
            _chepInstance.transform.localRotation = Quaternion.identity;
            StripPlacementComponents(_chepInstance);
            MeasurePalletYawCorrection(_chepInstance);
        }
    }

    /// <summary>Floor + back wall behind the pallet, parented to the RIG (not the pivot) so they never
    /// spin or tilt with the pallet — just a static backdrop that gives the rotating pallet a shadow to
    /// cast, for some depth instead of it floating in flat black.</summary>
    private void BuildBackdrop()
    {
        var backdropColor = new Color(0.11f, 0.14f, 0.18f, 1f); // a touch darker than the camera's clear color so the pallet still reads as the subject

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "PreviewGround";
        UnityEngine.Object.Destroy(ground.GetComponent<Collider>());
        ground.transform.SetParent(_rigRoot.transform, false);
        ground.transform.localPosition = Vector3.zero;
        ground.transform.localScale = new Vector3(0.35f, 1f, 0.35f); // Unity's default Plane is 10x10 units
        var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = backdropColor };
        var groundRenderer = ground.GetComponent<MeshRenderer>();
        groundRenderer.sharedMaterial = groundMat;
        groundRenderer.receiveShadows = true;
        groundRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
        wall.name = "PreviewWall";
        UnityEngine.Object.Destroy(wall.GetComponent<Collider>());
        wall.transform.SetParent(_rigRoot.transform, false);
        wall.transform.localPosition = new Vector3(0f, 1.5f, 1.6f); // behind the pallet, relative to the camera at z=-2.6
        wall.transform.localScale = new Vector3(3.5f, 3.5f, 1f);
        var wallMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = backdropColor };
        // Two-sided so the wall reads correctly regardless of the Quad primitive's default facing —
        // safer than guessing the exact rotation needed without a live Editor to check against.
        if (wallMat.HasProperty("_Cull")) wallMat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        var wallRenderer = wall.GetComponent<MeshRenderer>();
        wallRenderer.sharedMaterial = wallMat;
        wallRenderer.receiveShadows = true;
        wallRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>Measures the CHEP visual's actual footprint (world-space renderer bounds, taken right
    /// after instantiation before any spin/drag has rotated the pivot) and compares it against
    /// PalletBuilder's assumed layout axes (X=width/48", Z=length/40" — see PalletBuilder.palletDimensions).
    /// If the mesh's real footprint is swapped (long side on Z instead of X), the case layer built by a
    /// same-parented PalletBuilder would come out rotated 90° from the actual pallet shape — cases
    /// hanging off the corners at an angle. Storing a corrective yaw here means Generate Preview always
    /// aligns the case grid to the real mesh instead of assuming it already matches.</summary>
    private void MeasurePalletYawCorrection(GameObject chepInstance)
    {
        _palletYawCorrectionDeg = 0f;
        var renderers = chepInstance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        float assumedWidth = 1.2192f;  // PalletBuilder.palletDimensions.x (48")
        float assumedLength = 1.016f;  // PalletBuilder.palletDimensions.z (40")

        // If the mesh's actual X/Z extents line up better with the SWAPPED assumption than the
        // straight one, the pallet is authored 90° from what PalletBuilder expects.
        float straightError = Mathf.Abs(bounds.size.x - assumedWidth) + Mathf.Abs(bounds.size.z - assumedLength);
        float swappedError = Mathf.Abs(bounds.size.x - assumedLength) + Mathf.Abs(bounds.size.z - assumedWidth);

        if (swappedError < straightError) _palletYawCorrectionDeg = 90f;
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
        _liveCaseSingle.transform.localRotation = Quaternion.Euler(0f, _palletYawCorrectionDeg, 0f);

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
        float w = _widthField.value, h = _heightField.value, l = _lengthField.value;
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
        // Re-applied every click (not just on first creation) so the case layer always matches the
        // measured pallet footprint — see MeasurePalletYawCorrection.
        _previewPalletBuilder.transform.localRotation = Quaternion.Euler(0f, _palletYawCorrectionDeg, 0f);

        _previewPalletBuilder.casePrefab = _caseTemplate;
        _previewPalletBuilder.linkedSku = null;
        _previewPalletBuilder.caseDimensions = new Vector3(w, h, l);
        _previewPalletBuilder.useTiHiOverride = true;
        _previewPalletBuilder.manualTi = ti;
        _previewPalletBuilder.manualHi = hi;
        _previewPalletBuilder.Build(deductMoney: false);

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
                _widthField.value, _heightField.value, _lengthField.value,
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
        so.FindProperty("_caseLength").floatValue = _lengthField.value;
        so.FindProperty("_caseWidth").floatValue = _widthField.value;
        so.FindProperty("_caseHeight").floatValue = _heightField.value;
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

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = safeName + "_Mat" };
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
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
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

        tapeMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name + "_Tape" };
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
            labelMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name + "_Label" };
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

        var header = MakeText(title, 13, ColTitleText, bold: true);
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
        ApplyFont(field);
        field.style.marginBottom = 6;
        var label = field.Q<Label>();
        if (label != null)
        {
            label.style.color = new StyleColor(ColSubtleText);
            label.style.minWidth = 150;
        }
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
