using System;
using System.Collections.Generic;
using GameCore.Economy;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// First-draft Shift Manager — lets the player define named shifts (e.g. "Day Shift", "Clean
/// Shift", "Night Shift"), each with a Start/End time per day of week, or "Closed" for days with
/// no shift that week. Bound to the "5" key (see TopBarUI).
///
/// SCOPE NOTE (explicit, per Tad 2026-06-25/26): this is UI-only for now. Nothing here drives
/// actual employee arrival/departure/overtime/attendance yet — EmployeeRecord.shift and
/// ShiftSchedule.cs (Day/Evening/Night/Flexible, fixed windows) are UNCHANGED and still what
/// actually runs PayrollService's overtime math. Schedules built here are also NOT persisted to
/// disk/save files yet — they live in memory for the current session only. Wiring this into the
/// real employee schedule system and adding save-file persistence are explicitly deferred
/// follow-up work (a planned "side UI" for assigning individual employees to one of these named
/// shifts is the next piece — not built yet).
///
/// Day-of-week index here is Sun=0..Sat=6 (matches the mockup's left-to-right day order).
/// EmployeeWorkSchedule elsewhere uses Mon=0..Sun=6 — these will need reconciling when this
/// system is actually wired into gameplay.
///
/// Implements IUIPanel for keybinding exclusivity via UIKeyBindingManager.
/// </summary>
public class ShiftManagerPanel : IUIPanel
{
    private static readonly string[] DayNames = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
    private const int Closed = -1;
    private const int NotSet = -2;
    private const float DayColWidth = 160f;
    private const float LabelColWidth = 140f;
    private const float SeparatorWidth = 3f;
    private const int StandardShiftMinutes = 510; // 8.5 hours — standard no-OT shift length

    // ── Palette — matches Employee Roster / Hiring Board (navy/blue + game orange accents) ──
    private static readonly Color ColBg        = new Color(20f / 255f, 28f / 255f, 38f / 255f, 0.88f);
    private static readonly Color ColBorder    = new Color(0x5C / 255f, 0x9B / 255f, 0xC4 / 255f, 1f);
    private static readonly Color ColTitleText = new Color(0xCF / 255f, 0xE2 / 255f, 0xF0 / 255f, 1f);
    private static readonly Color ColSubtleText = new Color(0x7A / 255f, 0x99 / 255f, 0xB0 / 255f, 1f);
    private static readonly Color ColOrange     = new Color(0xB5 / 255f, 0x74 / 255f, 0x3A / 255f, 1f);
    private static readonly Color ColOrangeEdge = new Color(0x7A / 255f, 0x4C / 255f, 0x22 / 255f, 1f);
    private static readonly Color ColOrangeText = new Color(0xFD / 255f, 0xE8 / 255f, 0xCC / 255f, 1f);
    private static readonly Color ColOrangeHover = new Color(0xC6 / 255f, 0x7F / 255f, 0x42 / 255f, 1f);
    private static readonly Color ColDayHeader  = ColOrange;
    private static readonly Color ColLabelCell  = new Color(0x4C / 255f, 0x90 / 255f, 0xC0 / 255f, 1f);
    private static readonly Color ColBlueEdge   = new Color(0x2C / 255f, 0x5E / 255f, 0x82 / 255f, 1f);
    private static readonly Color ColBlueHover  = new Color(0x5B / 255f, 0xA6 / 255f, 0xD8 / 255f, 1f);
    private static readonly Color ColCellEven   = new Color(36f / 255f, 48f / 255f, 62f / 255f, 0.65f);
    private static readonly Color ColCellOdd    = new Color(30f / 255f, 40f / 255f, 52f / 255f, 0.65f);
    private static readonly Color ColTodayTint  = new Color(0.85f, 0.70f, 0.30f, 0.55f);
    private static readonly Color ColCharcoal  = new Color(0x40 / 255f, 0x40 / 255f, 0x40 / 255f, 0.75f); // charcoal black, semi-transparent
    private static readonly Color ColError      = ColCharcoal; // Use charcoal instead of red
    private static readonly Color ColErrorBorder = ColCharcoal; // Use charcoal instead of red
    private static readonly Color ColFireRed    = new Color(0xC1 / 255f, 0x27 / 255f, 0x2D / 255f, 1f);
    private static readonly Color ColFireRedEdge = new Color(0x7A / 255f, 0x16 / 255f, 0x1A / 255f, 1f);
    private static readonly Color ColFireRedHover = new Color(0xD8 / 255f, 0x3A / 255f, 0x40 / 255f, 1f);
    private static readonly Color ColVanilla    = new Color(0xF5 / 255f, 0xF0 / 255f, 0xE1 / 255f, 1f);

    private static readonly List<string> TimeChoices = BuildTimeChoices();

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

    private class ShiftRow
    {
        public TextField NameField;
        public Label NamePlaceholder;
        public Label StartLabel;
        public Label EndLabel;
        public readonly int[] Start = new int[7];
        public readonly int[] End   = new int[7];
        public readonly DropdownField[] StartDropdowns = new DropdownField[7];
        public readonly DropdownField[] EndDropdowns   = new DropdownField[7];
        // Decorative border-only overlays (position:absolute, no layout weight) drawn on top of
        // each dropdown — see BuildTimeRow for why the border can't live on the dropdown itself.
        public readonly VisualElement[] StartBorders = new VisualElement[7];
        public readonly VisualElement[] EndBorders   = new VisualElement[7];
        public VisualElement Block;
    }

    private readonly List<ShiftRow> _shifts = new();
    private readonly VisualElement _overlay;
    private readonly VisualElement _modal;
    private readonly VisualElement _shiftsContainer;
    private readonly VisualElement _confirmBlocker;
    private readonly Label _confirmMessage;
    private readonly Label[] _dayNumberLabels = new Label[7];
    private readonly ITimeService _timeService;
    private Action _confirmYesAction;
    private bool _visible;
    private bool _hasChanges;

    public ShiftManagerPanel(VisualElement root, ITimeService timeService)
    {
        _timeService = timeService;
        _overlay = Build(out _modal, out _shiftsContainer, out _confirmBlocker, out _confirmMessage);
        root.Add(_overlay);
        Hide();
    }

    /// <summary>Rebuilds the panel from ShiftDefinitionRegistry — the source of truth is the
    /// registry, not the panel's own UI state, so this always reflects whatever was last
    /// committed (including a just-restored save file). Called on every Show() rather than only
    /// in the constructor, since the panel instance is long-lived but a save/load can happen at
    /// any point after construction, well after this instance's own _shifts would otherwise be stale.
    /// Falls back to a single default "Day Shift" if the registry is empty (fresh session).</summary>
    private void LoadFromRegistry()
    {
        _shiftsContainer.Clear();
        _shifts.Clear();

        if (ShiftDefinitionRegistry.All.Count == 0)
        {
            AddShift("Day Shift");
            _hasChanges = false;
            return;
        }

        foreach (var def in ShiftDefinitionRegistry.All)
        {
            AddShift(def.Name);
            var shift = _shifts[_shifts.Count - 1];
            shift.NameField.SetValueWithoutNotify(def.Name);
            shift.NamePlaceholder.style.display = string.IsNullOrEmpty(def.Name) ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateTimeRowLabels(shift);

            for (int d = 0; d < 7; d++)
            {
                shift.Start[d] = def.Start[d];
                shift.End[d] = def.End[d];
                shift.StartDropdowns[d].SetValueWithoutNotify(DisplayForTime(def.Start[d]));
                shift.EndDropdowns[d].SetValueWithoutNotify(DisplayForTime(def.End[d]));
            }
            RefreshRowColors(shift);
        }
        _hasChanges = false;
    }

    private static string DisplayForTime(int val) => val == Closed ? "Closed" : val == NotSet ? "" : FormatTime(val);

    /// <summary>Pushes the panel's current shift list into ShiftDefinitionRegistry so
    /// PlacementSystem can snapshot it on the next save.</summary>
    private void CommitToRegistry()
    {
        var list = new List<ShiftDefinitionRegistry.ShiftDefinition>();
        foreach (var shift in _shifts)
        {
            var def = new ShiftDefinitionRegistry.ShiftDefinition { Name = shift.NameField.value?.Trim() };
            for (int d = 0; d < 7; d++)
            {
                def.Start[d] = shift.Start[d];
                def.End[d] = shift.End[d];
            }
            list.Add(def);
        }
        ShiftDefinitionRegistry.ReplaceAll(list);
    }

    public bool IsVisible => _visible;
    /// <summary>IUIPanel implementation: true if this panel is currently visible.</summary>
    public bool IsOpen => _visible;

    public void Toggle() { if (_visible) TryClose(); else Show(); }

    public void Show()
    {
        _visible = true;
        _overlay.style.display = DisplayStyle.Flex;
        _overlay.pickingMode = PickingMode.Position;
        LoadFromRegistry();
        RefreshDayNumbers();
    }

    private void MarkChanged() => _hasChanges = true;

    private void RefreshDayNumbers()
    {
        int todayIdx = EmployeeStatSystem.GetCurrentDayOfWeek(); // Mon=0..Sun=6
        int todaySunFirst = (todayIdx + 1) % 7;
        int currentDay = _timeService?.Day ?? 1;

        for (int d = 0; d < 7; d++)
        {
            int dayNumber = currentDay + (d - todaySunFirst);
            if (_dayNumberLabels[d] != null)
                _dayNumberLabels[d].text = $"Day {dayNumber:00}";
        }
    }

    public void Dispose()
    {
        if (_overlay.parent != null) _overlay.RemoveFromHierarchy();
    }

    public void Hide()
    {
        _visible = false;
        _overlay.style.display = DisplayStyle.None;
        _overlay.pickingMode = PickingMode.Ignore;
        HideConfirmation();
    }

    public void TryClose()
    {
        if (!_hasChanges)
        {
            Hide();
            return;
        }

        if (!ValidateAll(out string error))
        {
            UIToast.Show(error, 2.5f);
            return;
        }
        CommitToRegistry();
        UIToast.Show("Shift schedule saved.", 1.5f);
        _hasChanges = false;
        Hide();
    }

    /// <summary>The ✕ button is a cancel/discard action, distinct from Save & Close — it skips
    /// validation entirely and just confirms before throwing away any unsaved changes.</summary>
    private void OnCloseButtonClicked()
    {
        ShowConfirmation("Exit? Changes may be lost.", Hide);
    }

    /// <summary>True while the panel holds edits the player hasn't saved or discarded.</summary>
    public bool HasUnsavedChanges => _hasChanges;

    /// <summary>
    /// Close on someone else's behalf — used when opening another panel would otherwise yank this one
    /// away. Untouched, it just closes. Edited, it raises the existing confirmation and only closes
    /// (and only then runs <paramref name="onClosed"/>) if the player says yes, so a stray keypress
    /// can't silently discard a schedule someone was halfway through building.
    ///
    /// Deliberately reuses the same ShowConfirmation the ✕ button uses rather than adding a second
    /// dialog — the confirm UI was already built, it just had no route in from outside.
    /// </summary>
    public void RequestClose(Action onClosed)
    {
        if (!_hasChanges)
        {
            Hide();
            onClosed?.Invoke();
            return;
        }

        ShowConfirmation("Are you sure? Exit — changes may be lost.", () =>
        {
            Hide();
            onClosed?.Invoke();
        });
    }

    // ── Validation ──────────────────────────────────────────────────────────────
    private bool ValidateAll(out string error)
    {
        foreach (var shift in _shifts)
        {
            if (string.IsNullOrWhiteSpace(shift.NameField.value))
            {
                error = "Shift Name is Required";
                return false;
            }

            if (!NormalizeAndCheckTimes(shift))
            {
                error = "Please correct any errors before submitting the Schedule";
                return false;
            }
        }

        // Duplicate names across all shifts.
        for (int i = 0; i < _shifts.Count; i++)
            for (int j = i + 1; j < _shifts.Count; j++)
                if (string.Equals(_shifts[i].NameField.value?.Trim(), _shifts[j].NameField.value?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    error = "Shift Name in Use";
                    return false;
                }

        foreach (var shift in _shifts) RefreshRowColors(shift);
        error = null;
        return true;
    }

    /// <summary>
    /// Resolves any day where BOTH start and end were never touched into an explicit "Closed"
    /// (this is the "simplify the process" default). Returns false if any day is left in an
    /// inconsistent state (one side set, the other blank, or a start time after its end time).
    /// </summary>
    private bool NormalizeAndCheckTimes(ShiftRow shift)
    {
        bool ok = true;
        for (int d = 0; d < 7; d++)
        {
            bool startNotSet = shift.Start[d] == NotSet;
            bool endNotSet = shift.End[d] == NotSet;

            if (startNotSet && endNotSet)
            {
                shift.Start[d] = Closed;
                shift.End[d] = Closed;
                shift.StartDropdowns[d].SetValueWithoutNotify("Closed");
                shift.EndDropdowns[d].SetValueWithoutNotify("Closed");
                continue;
            }

            if (startNotSet != endNotSet) { ok = false; continue; }

            if (shift.Start[d] >= 0 && shift.End[d] >= 0 && shift.Start[d] > shift.End[d])
                ok = false;
        }
        return ok;
    }

    // ── Time choice list: "Closed" + every 30 minutes from 0:00 to 23:30 ────────
    private static List<string> BuildTimeChoices()
    {
        var list = new List<string> { "Closed" };
        for (int m = 0; m < 1440; m += 30)
            list.Add(FormatTime(m));
        return list;
    }

    private static string FormatTime(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";

    private static int ParseTime(string s)
    {
        if (string.IsNullOrEmpty(s)) return NotSet;
        if (s == "Closed") return Closed;
        var parts = s.Split(':');
        return int.Parse(parts[0]) * 60 + int.Parse(parts[1]);
    }

    // ── Build shell (overlay + draggable modal + header + confirmation modal) ──
    private VisualElement Build(out VisualElement modal, out VisualElement shiftsContainer,
        out VisualElement confirmBlocker, out Label confirmMessage)
    {
        var overlay = new VisualElement { name = "shift-mgr-overlay" };
        overlay.style.position = Position.Absolute;
        // Stops above the bottom HUD so this scrim can't swallow clicks on the bar or the Build/Play
        // tabs — see the note on workqueue-overlay in WorkQueuePanel.Build.
        overlay.style.left = 0; overlay.style.top = 0; overlay.style.right = 0;
        overlay.style.bottom = BuildMenuUI.BottomHudReservedHeight;
        overlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.55f));
        // Anchored toward the top (not vertically centered) and horizontally centered, so the
        // panel has room below it to grow downward as shifts are added instead of running out
        // of screen space.
        overlay.style.justifyContent = Justify.FlexStart;
        overlay.style.alignItems = Align.Center;

        modal = new VisualElement { name = "shift-mgr-modal" };
        modal.style.position = Position.Absolute;
        modal.style.left = 100;
        modal.style.top = 100;
        modal.style.backgroundColor = new StyleColor(ColBg);
        modal.style.borderTopWidth = 3; modal.style.borderBottomWidth = 3;
        modal.style.borderLeftWidth = 3; modal.style.borderRightWidth = 3;
        modal.style.borderTopColor = new StyleColor(ColBorder);
        modal.style.borderBottomColor = new StyleColor(ColBorder);
        modal.style.borderLeftColor = new StyleColor(ColBorder);
        modal.style.borderRightColor = new StyleColor(ColBorder);
        modal.style.borderTopLeftRadius = 16; modal.style.borderTopRightRadius = 16;
        modal.style.borderBottomLeftRadius = 16; modal.style.borderBottomRightRadius = 16;
        modal.style.paddingTop = 14; modal.style.paddingBottom = 14;
        modal.style.paddingLeft = 16; modal.style.paddingRight = 16;
        // Sized tightly to the pinned grid width (+ padding) — the old 25%-extra buffer
        // predates the wider columns/separators added since, and now just leaves a dead gap
        // between the grid's right edge and the modal's border.
        modal.style.minWidth = LabelColWidth + DayColWidth * 7 + SeparatorWidth * 6 + 32f;
        // No maxHeight/scroll here on purpose — adding shifts should grow the panel to fit
        // every record at full, unsquished scale rather than shrinking rows to stay capped.
        modal.style.flexShrink = 0;

        // ── Title bar (draggable handle) — title centered, close button pinned right ──
        var titleBar = new VisualElement();
        titleBar.style.flexDirection = FlexDirection.Row;
        titleBar.style.alignItems = Align.Center;
        titleBar.style.marginBottom = 10;

        // Spacer the same width as the close button balances the close button's width
        // so the centered title isn't visually pushed off-center.
        var titleSpacer = new VisualElement();
        titleSpacer.style.width = 28;
        titleBar.Add(titleSpacer);

        var title = new Label("Shift Manager");
        ApplyFont(title, bold: true, size: 30);
        title.style.color = new StyleColor(ColTitleText);
        title.style.flexGrow = 1;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleBar.Add(title);

        var closeButton = new Button(OnCloseButtonClicked) { text = "✕" };
        closeButton.style.width = 28; closeButton.style.height = 28;
        closeButton.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));
        closeButton.style.color = new StyleColor(ColSubtleText);
        closeButton.style.borderTopWidth = closeButton.style.borderBottomWidth = 1;

        // Add red hover effect like other close buttons
        closeButton.RegisterCallback<PointerEnterEvent>(_ =>
        {
            closeButton.style.backgroundColor = new StyleColor(new Color(0xE6 / 255f, 0x50 / 255f, 0x50 / 255f, 0.3f));
            closeButton.style.color = new StyleColor(Color.white);
        });
        closeButton.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            closeButton.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));
            closeButton.style.color = new StyleColor(ColSubtleText);
        });

        titleBar.Add(closeButton);
        modal.Add(titleBar);

        new DraggableWindow(modal, titleBar, closeButton);

        // ── Day header row (shared across all shifts) ───────────────────────
        modal.Add(BuildDayHeaderRow());

        // ── Shifts container (one block per shift) ───────────────────────────
        shiftsContainer = new VisualElement();
        shiftsContainer.style.flexShrink = 0;
        modal.Add(shiftsContainer);

        // ── Footer: Add Shift / Save ─────────────────────────────────────────
        var footer = new VisualElement();
        footer.style.flexDirection = FlexDirection.Row;
        footer.style.justifyContent = Justify.SpaceBetween;
        footer.style.marginTop = 12;

        var addButton = StyleOrangeButton(new Button(() => AddShift("New Shift")) { text = "+ Add Shift" });
        var saveButton = StyleButton(new Button(TryClose) { text = "Save & Close" }, ColFireRed, ColFireRedEdge, ColVanilla, ColFireRedHover);
        // ~25% bigger than the standard button sizing.
        saveButton.style.fontSize = 19;
        saveButton.style.paddingTop = 8; saveButton.style.paddingBottom = 8;
        saveButton.style.paddingLeft = 18; saveButton.style.paddingRight = 18;
        footer.Add(addButton);
        footer.Add(saveButton);
        modal.Add(footer);

        overlay.Add(modal);

        // ── Confirmation sub-modal (Remove confirmation) ─────────────────────
        confirmBlocker = new VisualElement { name = "shift-mgr-confirm-blocker" };
        confirmBlocker.style.position = Position.Absolute;
        confirmBlocker.style.left = 0; confirmBlocker.style.top = 0; confirmBlocker.style.right = 0; confirmBlocker.style.bottom = 0;
        confirmBlocker.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.45f));
        confirmBlocker.style.justifyContent = Justify.Center;
        confirmBlocker.style.alignItems = Align.Center;
        confirmBlocker.style.display = DisplayStyle.None;

        var confirmBox = new VisualElement();
        confirmBox.style.backgroundColor = new StyleColor(ColBg);
        confirmBox.style.borderTopWidth = confirmBox.style.borderBottomWidth = 3;
        confirmBox.style.borderLeftWidth = confirmBox.style.borderRightWidth = 3;
        confirmBox.style.borderTopColor = confirmBox.style.borderBottomColor =
            confirmBox.style.borderLeftColor = confirmBox.style.borderRightColor = new StyleColor(ColBorder);
        confirmBox.style.borderTopLeftRadius = confirmBox.style.borderTopRightRadius =
            confirmBox.style.borderBottomLeftRadius = confirmBox.style.borderBottomRightRadius = 14;
        confirmBox.style.paddingTop = confirmBox.style.paddingBottom = 18;
        confirmBox.style.paddingLeft = confirmBox.style.paddingRight = 24;
        confirmBox.style.minWidth = 280;
        confirmBox.style.alignItems = Align.Center;

        var confirmTitle = new Label("Confirmation");
        ApplyFont(confirmTitle, bold: true, size: 20);
        confirmTitle.style.color = new StyleColor(ColTitleText);
        confirmTitle.style.marginBottom = 8;
        confirmBox.Add(confirmTitle);

        confirmMessage = new Label("Are you sure?");
        ApplyFont(confirmMessage, bold: false, size: 16);
        confirmMessage.style.color = new StyleColor(ColTitleText);
        confirmMessage.style.marginBottom = 14;
        confirmBox.Add(confirmMessage);

        var confirmRow = new VisualElement();
        confirmRow.style.flexDirection = FlexDirection.Row;
        confirmRow.style.justifyContent = Justify.Center;

        var yesButton = StyleOrangeButton(new Button(() => { _confirmYesAction?.Invoke(); HideConfirmation(); }) { text = "Yes" });
        yesButton.style.marginRight = 10;
        var noButton = StyleBlueButton(new Button(HideConfirmation) { text = "No" });

        confirmRow.Add(yesButton);
        confirmRow.Add(noButton);
        confirmBox.Add(confirmRow);

        confirmBlocker.Add(confirmBox);
        overlay.Add(confirmBlocker);

        return overlay;
    }

    private void ShowConfirmation(string message, Action onYes)
    {
        _confirmMessage.text = message;
        _confirmYesAction = onYes;
        _confirmBlocker.style.display = DisplayStyle.Flex;
    }

    private void HideConfirmation()
    {
        _confirmBlocker.style.display = DisplayStyle.None;
        _confirmYesAction = null;
    }

    private Button StyleOrangeButton(Button b) => StyleButton(b, ColOrange, ColOrangeEdge, ColOrangeText, ColOrangeHover);
    private Button StyleBlueButton(Button b) => StyleButton(b, ColLabelCell, ColBlueEdge, Color.white, ColBlueHover);

    private Button StyleButton(Button b, Color bg, Color edge, Color text, Color hover)
    {
        ApplyFont(b, bold: true, size: 15);
        b.style.backgroundColor = new StyleColor(bg);
        b.style.color = new StyleColor(text);
        b.style.borderBottomWidth = 3;
        b.style.borderBottomColor = new StyleColor(edge);
        b.style.borderTopWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 0;
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 8;
        b.style.paddingTop = 6; b.style.paddingBottom = 6;
        b.style.paddingLeft = 14; b.style.paddingRight = 14;
        b.RegisterCallback<PointerEnterEvent>(_ => b.style.backgroundColor = new StyleColor(hover));
        b.RegisterCallback<PointerLeaveEvent>(_ => b.style.backgroundColor = new StyleColor(bg));
        return b;
    }

    private VisualElement BuildDayHeaderRow()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        // Pin the header's total width to exactly match the data grid below (which is wider
        // by 6 * SeparatorWidth thanks to the blue separator lines between cells) — otherwise
        // this row defaults to stretching across the modal's full (wider) content box and the
        // orange banner falls short of the grid's right edge.
        row.style.width = LabelColWidth + DayColWidth * 7 + SeparatorWidth * 6;

        var corner = new VisualElement();
        corner.style.width = LabelColWidth;
        row.Add(corner);

        for (int d = 0; d < 7; d++)
        {
            var cell = new VisualElement();
            cell.style.width = DayColWidth;
            cell.style.flexDirection = FlexDirection.Column;
            cell.style.alignItems = Align.Center;
            cell.style.backgroundColor = new StyleColor(ColDayHeader);
            cell.style.paddingTop = 5; cell.style.paddingBottom = 5;

            var dayLabel = new Label(DayNames[d]);
            ApplyFont(dayLabel, bold: true, size: 15);
            dayLabel.style.color = new StyleColor(ColOrangeText);
            dayLabel.style.width = Length.Percent(100);
            dayLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            cell.Add(dayLabel);

            var dayNumberLabel = new Label("Day 00");
            ApplyFont(dayNumberLabel, bold: false, size: 11);
            dayNumberLabel.style.color = new StyleColor(ColOrangeText);
            dayNumberLabel.style.width = Length.Percent(100);
            dayNumberLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            cell.Add(dayNumberLabel);
            _dayNumberLabels[d] = dayNumberLabel;

            row.Add(cell);

            // Orange filler matching the grid's separator gaps, so the banner's colored area
            // spans the same width as the data cells below with no dark gaps in between.
            if (d < 6)
            {
                var fillerSpacer = new VisualElement();
                fillerSpacer.style.width = SeparatorWidth;
                fillerSpacer.style.backgroundColor = new StyleColor(ColDayHeader);
                row.Add(fillerSpacer);
            }
        }
        return row;
    }

    // ── Per-shift block: name field + Enter Info / Remove + Start row + End row ─
    private void AddShift(string defaultName)
    {
        MarkChanged();
        var shift = new ShiftRow();
        for (int d = 0; d < 7; d++) { shift.Start[d] = NotSet; shift.End[d] = NotSet; }

        var block = new VisualElement();
        block.style.marginTop = 10;
        block.style.borderTopWidth = 1;
        block.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.10f));
        block.style.paddingTop = 8;
        block.style.flexShrink = 0;

        // Name + Enter Info (left) ... Remove (right, flush with the day grid below — the
        // modal itself is wider than the grid, so capping this row's width to the grid's own
        // width keeps Remove from floating out in that dead margin).
        var nameRow = new VisualElement();
        nameRow.style.flexDirection = FlexDirection.Row;
        nameRow.style.alignItems = Align.Center;
        nameRow.style.justifyContent = Justify.SpaceBetween;
        nameRow.style.marginBottom = 6;
        nameRow.style.width = LabelColWidth + DayColWidth * 7 + SeparatorWidth * 6;

        var leftGroup = new VisualElement();
        leftGroup.style.flexDirection = FlexDirection.Row;
        leftGroup.style.alignItems = Align.Center;

        var nameWrap = new VisualElement();
        nameWrap.style.position = Position.Relative;
        nameWrap.style.marginRight = 8;

        shift.NameField = new TextField { value = "" };
        ApplyFont(shift.NameField, bold: false, size: 15);
        shift.NameField.style.width = 220;
        // Typed text matches the placeholder's color/opacity (same blue as the Start:/End:
        // labels) — the field's inner input area renders on a light background, so a light
        // text color was unreadably faint.
        shift.NameField.style.color = new StyleColor(ColLabelCell);
        shift.NameField.style.borderTopWidth = shift.NameField.style.borderBottomWidth =
            shift.NameField.style.borderLeftWidth = shift.NameField.style.borderRightWidth = 1;
        shift.NameField.style.borderTopColor = shift.NameField.style.borderBottomColor =
            shift.NameField.style.borderLeftColor = shift.NameField.style.borderRightColor = new StyleColor(ColLabelCell);

        var nameInner = shift.NameField.Q(className: "unity-base-text-field__input");
        if (nameInner != null) nameInner.style.color = new StyleColor(ColLabelCell);

        shift.NamePlaceholder = new Label("Shift Name (req.)");
        ApplyFont(shift.NamePlaceholder, bold: false, size: 15);
        shift.NamePlaceholder.style.position = Position.Absolute;
        shift.NamePlaceholder.style.left = 6; shift.NamePlaceholder.style.top = 3;
        shift.NamePlaceholder.style.color = new StyleColor(ColLabelCell);
        shift.NamePlaceholder.pickingMode = PickingMode.Ignore;

        shift.NameField.RegisterValueChangedCallback(evt =>
        {
            shift.NamePlaceholder.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateTimeRowLabels(shift);
            MarkChanged();
        });

        nameWrap.Add(shift.NameField);
        nameWrap.Add(shift.NamePlaceholder);
        leftGroup.Add(nameWrap);

        var enterInfoButton = StyleBlueButton(new Button(() => OnEnterInfo(shift)) { text = "Enter Info" });
        leftGroup.Add(enterInfoButton);

        var removeButton = StyleOrangeButton(new Button(() =>
            ShowConfirmation("Are you sure?", () => RemoveShift(shift, block))) { text = "Remove" });

        nameRow.Add(leftGroup);
        nameRow.Add(removeButton);
        block.Add(nameRow);

        block.Add(BuildTimeRow("Start", shift, isStart: true));
        block.Add(BuildTimeRow("End", shift, isStart: false));

        shift.Block = block;
        _shifts.Add(shift);
        _shiftsContainer.Add(block);
        RefreshRowColors(shift);
    }

    private void OnEnterInfo(ShiftRow shift)
    {
        string name = shift.NameField.value?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            UIToast.Show("Shift Name is Required", 2f);
            return;
        }

        foreach (var other in _shifts)
        {
            if (other == shift) continue;
            if (string.Equals(other.NameField.value?.Trim(), name, StringComparison.OrdinalIgnoreCase))
            {
                UIToast.Show("Shift Name in Use", 2f);
                return;
            }
        }

        bool ok = NormalizeAndCheckTimes(shift);
        RefreshRowColors(shift);

        if (!ok)
        {
            UIToast.Show("Please correct any errors before submitting the Schedule", 2.5f);
            return;
        }

        shift.NameField.SetValueWithoutNotify(name);
        CommitToRegistry();
        UIToast.Show($"\"{name}\" shift info saved.", 1.5f);
    }

    private void RemoveShift(ShiftRow shift, VisualElement block)
    {
        MarkChanged();
        _shifts.Remove(shift);
        block.RemoveFromHierarchy();
    }

    private VisualElement BuildTimeRow(string rowLabel, ShiftRow shift, bool isStart)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        // Pinned to the exact same total width as the header banner and the name/Remove row,
        // so all rows line up pixel-for-pixel instead of each defaulting to its own stretch
        // behavior against the (wider) modal.
        row.style.width = LabelColWidth + DayColWidth * 7 + SeparatorWidth * 6;

        var label = new Label($"{rowLabel}:");
        ApplyFont(label, bold: true, size: 15);
        label.style.width = LabelColWidth;
        label.style.backgroundColor = new StyleColor(ColLabelCell);
        label.style.color = new StyleColor(Color.white);
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.paddingLeft = 6;
        row.Add(label);

        if (isStart) shift.StartLabel = label;
        else shift.EndLabel = label;

        int todayIdx = EmployeeStatSystem.GetCurrentDayOfWeek(); // Mon=0..Sun=6 — see ToSunFirst below
        int todaySunFirst = (todayIdx + 1) % 7; // convert Mon=0..Sun=6 -> Sun=0..Sat=6

        for (int d = 0; d < 7; d++)
        {
            int day = d; // capture for closures

            // Fixed-width wrapper carries the column's layout weight; the dropdown inside just
            // fills it. DropdownField has its own built-in min-width (driven by its content —
            // the popup arrow plus the longest visible time string), which at this font size was
            // winning over our explicit width and pushing each column wider than its sibling
            // header cell, compounding across 7 columns into a visible rightward drift between
            // the grid and the banner above it. minWidth=0 + overflow:Hidden on the wrapper
            // forces it back down to exactly DayColWidth, clipping rather than expanding if
            // content ever wants more room. The border also moved off the dropdown onto a
            // decorative position:absolute overlay so it can never itself contribute to layout
            // width either.
            var cellWrapper = new VisualElement();
            cellWrapper.style.width = DayColWidth;
            cellWrapper.style.position = Position.Relative;
            cellWrapper.style.overflow = Overflow.Hidden;

            var dropdown = new DropdownField(TimeChoices, 0);
            ApplyFont(dropdown, bold: false, size: 19);
            dropdown.style.width = Length.Percent(100);
            dropdown.style.minWidth = 0;
            dropdown.style.flexShrink = 1;
            // The wrapper is a column-flow container, so its single child doesn't auto-stretch
            // along the vertical (main) axis the way width:100% does on the horizontal (cross)
            // axis — without flexGrow, the dropdown's own fixed control height left a gap below
            // it whenever the wrapper ended up taller (e.g. stretched to match a taller sibling
            // cell in the same row), showing as a notch of bare background under the border.
            dropdown.style.flexGrow = 1;
            dropdown.SetValueWithoutNotify("");

            var dropdownText = dropdown.Q(className: "unity-base-popup-field__text");
            if (dropdownText != null)
            {
                ApplyFont(dropdownText, bold: false, size: 19);
                dropdownText.style.color = new StyleColor(ColLabelCell);
            }

            cellWrapper.Add(dropdown);

            var borderOverlay = new VisualElement { pickingMode = PickingMode.Ignore };
            borderOverlay.style.position = Position.Absolute;
            borderOverlay.style.left = 0; borderOverlay.style.top = 0;
            borderOverlay.style.right = 0; borderOverlay.style.bottom = 0;
            borderOverlay.style.borderTopWidth = borderOverlay.style.borderBottomWidth =
                borderOverlay.style.borderLeftWidth = borderOverlay.style.borderRightWidth = 1;
            borderOverlay.style.borderTopColor = borderOverlay.style.borderBottomColor =
                borderOverlay.style.borderLeftColor = borderOverlay.style.borderRightColor = new StyleColor(ColLabelCell);
            cellWrapper.Add(borderOverlay);

            if (isStart)
            {
                shift.StartDropdowns[day] = dropdown;
                shift.StartBorders[day] = borderOverlay;
                dropdown.RegisterValueChangedCallback(evt => OnStartChanged(shift, day, evt, todaySunFirst));
            }
            else
            {
                shift.EndDropdowns[day] = dropdown;
                shift.EndBorders[day] = borderOverlay;
                dropdown.RegisterValueChangedCallback(evt => OnEndChanged(shift, day, evt, todaySunFirst));
            }

            row.Add(cellWrapper);

            // Separator between days — removed (no visual dividers needed)
            // if (d < 6) { ... }
        }
        return row;
    }

    /// <summary>Mirrors the shift name into the "Start:"/"End:" row labels, per Tad's request
    /// that the typed name be visible there too, not just in the name field itself.</summary>
    private void UpdateTimeRowLabels(ShiftRow shift)
    {
        string name = shift.NameField.value;
        string suffix = string.IsNullOrEmpty(name) ? "" : $" {name}";
        if (shift.StartLabel != null) shift.StartLabel.text = $"Start:{suffix}";
        if (shift.EndLabel != null) shift.EndLabel.text = $"End:{suffix}";
    }

    private void OnStartChanged(ShiftRow shift, int day, ChangeEvent<string> evt, int todaySunFirst)
    {
        if (day == todaySunFirst && evt.newValue != evt.previousValue)
        {
            shift.StartDropdowns[day].SetValueWithoutNotify(evt.previousValue);
            UIToast.Show("Changing the same day is not allowed — schedule changes need at least 24 hours notice.", 2.5f);
            return;
        }

        MarkChanged();
        int oldVal = shift.Start[day];
        int newVal = ParseTime(evt.newValue);
        shift.Start[day] = newVal;

        if (newVal == Closed)
        {
            // Closing the start for a day forces the end closed too — can't have an end with no start.
            shift.End[day] = Closed;
            shift.EndDropdowns[day].SetValueWithoutNotify("Closed");
        }
        else
        {
            // QoL: auto-populate End as 8.5 hours later (a standard no-OT shift) so the common
            // case needs no second click. Clamped to the last 30-minute slot of the day (23:30)
            // rather than wrapping past midnight, since End/Start are both single-day values here.
            int autoEnd = Mathf.Min(newVal + StandardShiftMinutes, 1410);
            shift.End[day] = autoEnd;
            shift.EndDropdowns[day].SetValueWithoutNotify(FormatTime(autoEnd));
        }

        RefreshRowColors(shift);
    }

    private void OnEndChanged(ShiftRow shift, int day, ChangeEvent<string> evt, int todaySunFirst)
    {
        if (day == todaySunFirst && evt.newValue != evt.previousValue)
        {
            shift.EndDropdowns[day].SetValueWithoutNotify(evt.previousValue);
            UIToast.Show("Changing the same day is not allowed — schedule changes need at least 24 hours notice.", 2.5f);
            return;
        }

        MarkChanged();
        int oldVal = shift.End[day];
        int newVal = ParseTime(evt.newValue);
        shift.End[day] = newVal;

        if (newVal == Closed)
        {
            shift.Start[day] = Closed;
            shift.StartDropdowns[day].SetValueWithoutNotify("Closed");
        }
        else if (oldVal == Closed)
        {
            shift.Start[day] = NotSet;
            shift.StartDropdowns[day].SetValueWithoutNotify("");
        }

        RefreshRowColors(shift);
    }

    // ── Visual feedback: blank = red .65, bad start/end order = red .66, else normal ──
    private void RefreshRowColors(ShiftRow shift)
    {
        int todayIdx = EmployeeStatSystem.GetCurrentDayOfWeek();
        int todaySunFirst = (todayIdx + 1) % 7;

        for (int d = 0; d < 7; d++)
        {
            bool startNotSet = shift.Start[d] == NotSet;
            bool endNotSet = shift.End[d] == NotSet;
            bool badOrder = !startNotSet && !endNotSet && shift.Start[d] >= 0 && shift.End[d] >= 0 && shift.Start[d] > shift.End[d];

            Color baseColor = d == todaySunFirst ? ColTodayTint : (d % 2 == 0 ? ColCellEven : ColCellOdd);

            bool startError = startNotSet || badOrder;
            bool endError = endNotSet || badOrder;

            shift.StartDropdowns[d].style.backgroundColor = new StyleColor(startError ? ColError : baseColor);
            shift.EndDropdowns[d].style.backgroundColor = new StyleColor(endError ? ColError : baseColor);

            Color startBorder = startError ? ColErrorBorder : ColLabelCell;
            var sb = shift.StartBorders[d];
            sb.style.borderTopColor = sb.style.borderBottomColor =
                sb.style.borderLeftColor = sb.style.borderRightColor = new StyleColor(startBorder);

            Color endBorder = endError ? ColErrorBorder : ColLabelCell;
            var eb = shift.EndBorders[d];
            eb.style.borderTopColor = eb.style.borderBottomColor =
                eb.style.borderLeftColor = eb.style.borderRightColor = new StyleColor(endBorder);
        }
    }
}
