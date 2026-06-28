using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Warehouse;

/// <summary>
/// Aisle Initialization modal. Uses the pallet-racking blueprint as the full modal background
/// (Resources/UI/RackingSchematic) with orange/Lilita controls overlaid, matching the game's
/// Employee Roster / Shift Manager aesthetic.
///
/// Hosted on the shared HUD UIDocument root (the one whose root contains "TopBar") rather than a
/// freshly-spawned UIDocument — a runtime UIDocument with no PanelSettings has a null panel and
/// renders nothing, which is why the first attempt was invisible.
///
/// Level rules (per Tad 2026-06-28): locations &lt;= 80" can be Pick OR Reserve (player chooses);
/// locations &gt; 80" are ALWAYS Reserve and locked (out of human reach). Side select is which side
/// of the aisle the order selector pulls from.
/// </summary>
public class AisleInitializationModal
{
    // ── Layout ──────────────────────────────────────────────────────────────────
    private const float ArtWidth = 640f;      // blueprint, right-anchored
    private const float GutterWidth = 250f;    // dark control column on the left
    private const float ModalHeight = 856f;

    // ── Palette (blueprint orange + navy, matches ShiftManagerPanel / Employee Roster) ──
    private static readonly Color ColOverlay     = new Color(0f, 0f, 0f, 0.60f);
    private static readonly Color ColModalBg      = new Color(20f / 255f, 28f / 255f, 38f / 255f, 0.96f);
    private static readonly Color ColBorder       = new Color(0xC9 / 255f, 0x86 / 255f, 0x3A / 255f, 1f);   // caramel
    private static readonly Color ColOrange       = new Color(0xF0 / 255f, 0x8C / 255f, 0x22 / 255f, 1f);   // blueprint orange
    private static readonly Color ColOrangeDim    = new Color(0xB5 / 255f, 0x74 / 255f, 0x3A / 255f, 1f);
    private static readonly Color ColOrangeText   = new Color(0xFD / 255f, 0xE8 / 255f, 0xCC / 255f, 1f);
    private static readonly Color ColWhite        = new Color(0.97f, 0.97f, 0.97f, 1f);
    private static readonly Color ColInputBg      = new Color(0.95f, 0.95f, 0.92f, 1f);
    private static readonly Color ColInputText    = new Color(0.08f, 0.08f, 0.08f, 1f);
    private static readonly Color ColLocked       = new Color(0x6A / 255f, 0x5A / 255f, 0x42 / 255f, 1f);   // dim, for >80" locked
    private static readonly Color ColSubmit       = new Color(0x2E / 255f, 0x8B / 255f, 0x3E / 255f, 1f);
    private static readonly Color ColCancel       = new Color(0x5A / 255f, 0x3A / 255f, 0x22 / 255f, 1f);

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

    private static void ApplyFont(VisualElement el, int size = -1)
    {
        var f = LilitaFont();
        if (f != null) el.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(f));
        if (size > 0) el.style.fontSize = size;
    }

    private class LevelRow
    {
        public int LevelNumber;          // 1 = bottom
        public float BeamHeightInches;
        public bool Locked;              // true when > 80" (forced Reserve)
        public DropdownField Dropdown;   // [Pick, Reserve] for selectable; disabled+Reserve when locked
    }

    private readonly VisualElement _overlay;
    private readonly List<RackLabelDisplay> _sections;
    private readonly Action<bool> _onClosed; // true = initialized, false = cancelled
    private readonly Vector3? _corridorCenter; // when opened from a chevron: cull labels toward the walkway
    private bool _closed;
    private DraggableWindow _drag;           // drag by the blueprint area

    private TextField _aisleField;
    private RadioButton _leftRadio, _rightRadio;
    private AisleSide _selectedSide = AisleSide.Right;
    private readonly List<LevelRow> _levelRows = new();

    public AisleInitializationModal(VisualElement root, List<RackLabelDisplay> sections, Action<bool> onClosed,
        Vector3? corridorCenter = null)
    {
        _sections = sections;
        _onClosed = onClosed;
        _corridorCenter = corridorCenter;

        _overlay = BuildOverlay();
        root.Add(_overlay);
    }

    // ── Build ───────────────────────────────────────────────────────────────────
    private VisualElement BuildOverlay()
    {
        var overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0; overlay.style.top = 0; overlay.style.right = 0; overlay.style.bottom = 0;
        overlay.style.backgroundColor = ColOverlay;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.alignItems = Align.Center;
        overlay.pickingMode = PickingMode.Position; // block clicks behind the modal

        // Modal = a left control GUTTER (dark navy) + the blueprint anchored to the RIGHT, so the
        // controls sit off the drawing. Width = Gutter + ArtWidth.
        var modal = new VisualElement();
        modal.style.width = GutterWidth + ArtWidth;
        modal.style.height = ModalHeight;
        modal.style.maxHeight = Length.Percent(94);
        modal.style.backgroundColor = ColModalBg;
        SetBorder(modal, ColBorder, 3);
        modal.style.borderTopLeftRadius = modal.style.borderTopRightRadius =
            modal.style.borderBottomLeftRadius = modal.style.borderBottomRightRadius = 10;

        // Blueprint art lives in its own right-anchored child so the modal's left gutter stays clear.
        var art = new VisualElement();
        art.style.position = Position.Absolute;
        art.style.top = 0; art.style.bottom = 0; art.style.right = 0;
        art.style.width = ArtWidth;
        art.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
        art.pickingMode = PickingMode.Position; // acts as the drag handle

        // Prefer the SVG however its importer generated it (VectorImage / Sprite); fall back to PNG.
        var vi = Resources.Load<VectorImage>("UI/RackingSchematic");
        var spr = Resources.Load<Sprite>("UI/RackingSchematic");
        var tex = Resources.Load<Texture2D>("UI/RackingSchematic");
        if (vi != null) art.style.backgroundImage = new StyleBackground(vi);
        else if (spr != null) art.style.backgroundImage = new StyleBackground(Background.FromSprite(spr));
        else if (tex != null) art.style.backgroundImage = new StyleBackground(tex);
        else Debug.LogWarning("[AisleModal] Resources/UI/RackingSchematic not found (VectorImage/Sprite/Texture2D).");

        modal.Add(art); // behind the controls
        overlay.Add(modal);

        BuildAisleField(modal);
        BuildSideSelect(modal);
        BuildLevelColumn(modal);
        BuildButtons(modal);

        // Drag the whole window by the blueprint area (controls in the gutter stay clickable).
        _drag = new DraggableWindow(modal, art, null);

        return overlay;
    }

    private void BuildAisleField(VisualElement modal)
    {
        var label = MakeLabel("AISLE:", 30, ColOrange);
        label.style.position = Position.Absolute;
        label.style.left = 18;
        label.style.top = 26;
        modal.Add(label);

        _aisleField = new TextField { maxLength = 2 };
        _aisleField.style.position = Position.Absolute;
        _aisleField.style.left = 145;
        _aisleField.style.top = 24;
        _aisleField.style.width = 80;
        _aisleField.style.height = 40;
        StyleInput(_aisleField);
        modal.Add(_aisleField);
    }

    private void BuildSideSelect(VisualElement modal)
    {
        var prompt = MakeLabel("Workers will pull\nfrom which side:", 22, ColOrange);
        prompt.style.position = Position.Absolute;
        prompt.style.left = 18;
        prompt.style.top = 95;
        prompt.style.whiteSpace = WhiteSpace.Normal;
        modal.Add(prompt);

        // Left option: orange arrow pointing LEFT (◄———) with the radio at the tail (right end).
        var leftRow = BuildArrowRadio(AisleSide.Left, out _leftRadio);
        leftRow.style.position = Position.Absolute;
        leftRow.style.left = 28;
        leftRow.style.top = 160;
        modal.Add(leftRow);

        // Right option: orange arrow pointing RIGHT (○———►) with the radio at the tail (left end).
        var rightRow = BuildArrowRadio(AisleSide.Right, out _rightRadio);
        rightRow.style.position = Position.Absolute;
        rightRow.style.left = 28;
        rightRow.style.top = 200;
        modal.Add(rightRow);

        SyncRadios(_selectedSide);
    }

    private VisualElement BuildArrowRadio(AisleSide side, out RadioButton radio)
    {
        bool pointLeft = side == AisleSide.Left;

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;

        // Arrowhead — default font (Lilita lacks these glyphs), orange.
        var head = new Label(pointLeft ? "◄" : "►"); // ◄ / ►
        head.style.color = ColOrange;
        head.style.fontSize = 26;

        // Shaft — a solid orange bar.
        var shaft = new VisualElement();
        shaft.style.height = 5;
        shaft.style.width = 70;
        shaft.style.backgroundColor = ColOrange;
        shaft.style.marginLeft = shaft.style.marginRight = 4;
        shaft.style.alignSelf = Align.Center;

        radio = new RadioButton();
        radio.style.color = ColOrange;
        var capturedSide = side;
        radio.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue) { _selectedSide = capturedSide; SyncRadios(capturedSide); }
        });

        // ◄———○  for Left (head, shaft, radio) ;  ○———►  for Right (radio, shaft, head)
        if (pointLeft) { row.Add(head); row.Add(shaft); row.Add(radio); }
        else           { row.Add(radio); row.Add(shaft); row.Add(head); }

        return row;
    }

    private void SyncRadios(AisleSide side)
    {
        _leftRadio?.SetValueWithoutNotify(side == AisleSide.Left);
        _rightRadio?.SetValueWithoutNotify(side == AisleSide.Right);
    }

    private void BuildLevelColumn(VisualElement modal)
    {
        // Detect levels from the gathered run (distinct heights, beam height + reach lock)
        var levels = AisleInitializer.DetectLevels(_sections)
            .Select(li => new LevelRow
            {
                LevelNumber = li.LevelNumber,
                BeamHeightInches = li.BeamHeightInches,
                Locked = li.Locked
            })
            .ToList();

        var column = new VisualElement();
        column.style.position = Position.Absolute;
        column.style.left = 16;
        column.style.top = 250;
        column.style.width = GutterWidth - 28;
        column.style.bottom = 95;            // stop above the button bar
        column.style.justifyContent = Justify.SpaceBetween;
        modal.Add(column);

        // Build rows top-down (highest level first) so Level 1 sits at the bottom
        for (int i = levels.Count - 1; i >= 0; i--)
        {
            var row = levels[i];
            column.Add(BuildLevelRow(row));
        }
    }

    private VisualElement BuildLevelRow(LevelRow row)
    {
        var rowEl = new VisualElement();
        rowEl.style.flexDirection = FlexDirection.Row;
        rowEl.style.alignItems = Align.Center;
        rowEl.style.marginBottom = 4;

        var lbl = MakeLabel($"Level {row.LevelNumber}:", 20, ColOrange);
        lbl.style.minWidth = 78;
        rowEl.Add(lbl);

        var dd = new DropdownField(new List<string> { "Pick", "Reserve" }, row.Locked ? 1 : 0);
        dd.style.width = 100;
        dd.style.height = 34;
        ApplyFont(dd, 18);
        if (row.Locked)
        {
            dd.SetValueWithoutNotify("Reserve");
            dd.SetEnabled(false);
            dd.tooltip = $"{row.BeamHeightInches:0}\" — above reach, Reserve only";
        }
        rowEl.Add(dd);

        row.Dropdown = dd;
        _levelRows.Add(row);
        return rowEl;
    }

    private void BuildButtons(VisualElement modal)
    {
        // Button bar pinned to the bottom of the left gutter (off the drawing).
        var bar = new VisualElement();
        bar.style.position = Position.Absolute;
        bar.style.left = 12;
        bar.style.width = GutterWidth - 20;
        bar.style.bottom = 24;
        bar.style.flexDirection = FlexDirection.Row;
        bar.style.justifyContent = Justify.SpaceBetween;
        modal.Add(bar);

        bar.Add(MakeButton("Cancel", ColCancel, Close));
        bar.Add(MakeButton("Submit", ColSubmit, Submit));
    }

    // ── Actions ──────────────────────────────────────────────────────────────────
    private void Submit()
    {
        if (!int.TryParse(_aisleField.value, out int aisle) || aisle < 1 || aisle > 99)
        {
            UIToast.Show("Enter a valid aisle number (01–99).", 2.5f);
            return;
        }

        if (WarehouseLocationsRegistry.Instance.IsAisleUsed(aisle))
        {
            UIToast.Show($"Aisle {aisle:D2} is already in use.", 2.5f);
            return;
        }

        // Build level configs bottom-up, assigning designations: picks count 1,2,3…; reserves
        // letter A,B,C… (starting from the first reserve above the picks).
        int pickCount = 0, reserveCount = 0;
        var configs = new List<LevelConfig>();
        foreach (var r in _levelRows.OrderBy(r => r.LevelNumber))
        {
            var type = r.Dropdown.value == "Reserve" ? LocationType.Reserve : LocationType.Pick;
            string designation = type == LocationType.Pick
                ? (++pickCount).ToString()
                : ((char)('A' + reserveCount++)).ToString();

            configs.Add(new LevelConfig { LevelNumber = r.LevelNumber, Type = type, Designation = designation });
        }

        var locations = AisleInitializer.InitializeAisle(_sections, aisle, _selectedSide, configs, _corridorCenter);
        WarehouseLocationsRegistry.Instance.AddLocations(locations);

        UIToast.Show($"Aisle {aisle:D2} initialized — {locations.Count} locations.", 2f);
        CloseInternal(true);
    }

    private void Close() => CloseInternal(false);

    private void CloseInternal(bool success)
    {
        if (_closed) return;
        _closed = true;
        if (_overlay.parent != null) _overlay.RemoveFromHierarchy();
        _onClosed?.Invoke(success);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────
    private Label MakeLabel(string text, int size, Color color)
    {
        var lbl = new Label(text) { text = text };
        lbl.style.color = color;
        ApplyFont(lbl, size);
        return lbl;
    }

    private Button MakeButton(string text, Color bg, Action onClick)
    {
        var btn = new Button(onClick) { text = text };
        btn.style.width = 106;
        btn.style.height = 46;
        btn.style.fontSize = 22;
        btn.style.color = ColOrangeText;
        btn.style.backgroundColor = bg;
        SetBorder(btn, ColBorder, 2);
        btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius =
            btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 8;
        ApplyFont(btn, 22);
        return btn;
    }

    private void StyleInput(TextField tf)
    {
        tf.style.fontSize = 26;
        var input = tf.Q(TextField.textInputUssName);
        if (input != null)
        {
            input.style.backgroundColor = ColInputBg;
            input.style.color = ColInputText;
            input.style.unityTextAlign = TextAnchor.MiddleCenter;
        }
        ApplyFont(tf, 26);
    }

    private static void SetBorder(VisualElement el, Color c, float w)
    {
        el.style.borderLeftColor = el.style.borderRightColor =
            el.style.borderTopColor = el.style.borderBottomColor = c;
        el.style.borderLeftWidth = el.style.borderRightWidth =
            el.style.borderTopWidth = el.style.borderBottomWidth = w;
    }

    /// <summary>Find the shared HUD UIDocument root (the one whose tree contains "TopBar").</summary>
    public static VisualElement FindHudRoot()
    {
        foreach (var d in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            if (d.rootVisualElement != null && d.rootVisualElement.Q("TopBar") != null)
                return d.rootVisualElement;
        }
        return null;
    }
}
