using System.Collections.Generic;
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
/// </summary>
public class ShiftManagerPanel
{
    private static readonly string[] DayNames = { "Sun", "Mon", "Tues", "Wed", "Thu", "Fri", "Sat" };
    private const int Closed = -1;
    private const float DayColWidth = 64f;
    private const float LabelColWidth = 110f;

    private static readonly Color ColBg        = new Color(0.98f, 0.98f, 0.96f, 1f);
    private static readonly Color ColBorder    = new Color(0.20f, 0.55f, 0.25f, 1f);
    private static readonly Color ColDayHeader = new Color(0.85f, 0.45f, 0.10f, 1f);
    private static readonly Color ColLabelCell = new Color(0.55f, 0.70f, 0.85f, 1f);
    private static readonly Color ColCellEven  = new Color(0.88f, 0.93f, 0.88f, 1f);
    private static readonly Color ColCellOdd   = new Color(0.93f, 0.96f, 0.93f, 1f);
    private static readonly Color ColTextDark  = new Color(0.10f, 0.10f, 0.10f, 1f);
    private static readonly Color ColTodayTint = new Color(0.95f, 0.85f, 0.85f, 1f);

    private static readonly List<string> TimeChoices = BuildTimeChoices();

    private class ShiftRow
    {
        public TextField NameField;
        public readonly int[] Start = new int[7];
        public readonly int[] End   = new int[7];
        public readonly DropdownField[] StartDropdowns = new DropdownField[7];
        public readonly DropdownField[] EndDropdowns   = new DropdownField[7];
        public VisualElement Block;
    }

    private readonly List<ShiftRow> _shifts = new();
    private readonly VisualElement _overlay;
    private readonly VisualElement _modal;
    private readonly VisualElement _shiftsContainer;
    private bool _visible;

    public ShiftManagerPanel(VisualElement root)
    {
        _overlay = Build(out _modal, out _shiftsContainer);
        root.Add(_overlay);
        Hide();

        AddShift("Day Shift");
    }

    public bool IsVisible => _visible;
    public void Toggle() { if (_visible) TryClose(); else Show(); }

    public void Show()
    {
        _visible = true;
        _overlay.style.display = DisplayStyle.Flex;
        _overlay.pickingMode = PickingMode.Position;
    }

    public void Dispose()
    {
        if (_overlay.parent != null) _overlay.RemoveFromHierarchy();
    }

    private void Hide()
    {
        _visible = false;
        _overlay.style.display = DisplayStyle.None;
        _overlay.pickingMode = PickingMode.Ignore;
    }

    private void TryClose()
    {
        if (!ValidateAll(out string error))
        {
            UIToast.Show(error, 2.5f);
            return;
        }
        UIToast.Show("Shift schedule saved.", 1.5f);
        Hide();
    }

    // ── Validation ──────────────────────────────────────────────────────────────
    private bool ValidateAll(out string error)
    {
        foreach (var shift in _shifts)
        {
            if (string.IsNullOrWhiteSpace(shift.NameField.value))
            {
                error = "Every shift needs a name before you can save.";
                return false;
            }

            for (int d = 0; d < 7; d++)
            {
                bool startSet = shift.Start[d] != Closed;
                bool endSet   = shift.End[d] != Closed;
                if (startSet != endSet)
                {
                    error = $"\"{shift.NameField.value}\" is missing a {(startSet ? "end" : "start")} time on {DayNames[d]} — fix it before saving.";
                    return false;
                }
            }
        }
        error = null;
        return true;
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
        if (s == "Closed" || string.IsNullOrEmpty(s)) return Closed;
        var parts = s.Split(':');
        return int.Parse(parts[0]) * 60 + int.Parse(parts[1]);
    }

    // ── Build shell (overlay + draggable modal + header) ────────────────────────
    private VisualElement Build(out VisualElement modal, out VisualElement shiftsContainer)
    {
        var overlay = new VisualElement { name = "shift-mgr-overlay" };
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0; overlay.style.top = 0; overlay.style.right = 0; overlay.style.bottom = 0;
        overlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.55f));
        overlay.style.justifyContent = Justify.Center;
        overlay.style.alignItems = Align.Center;

        modal = new VisualElement { name = "shift-mgr-modal" };
        modal.style.backgroundColor = new StyleColor(ColBg);
        modal.style.borderTopWidth = 3; modal.style.borderBottomWidth = 3;
        modal.style.borderLeftWidth = 3; modal.style.borderRightWidth = 3;
        modal.style.borderTopColor = new StyleColor(ColBorder);
        modal.style.borderBottomColor = new StyleColor(ColBorder);
        modal.style.borderLeftColor = new StyleColor(ColBorder);
        modal.style.borderRightColor = new StyleColor(ColBorder);
        modal.style.paddingTop = 10; modal.style.paddingBottom = 10;
        modal.style.paddingLeft = 12; modal.style.paddingRight = 12;
        modal.style.minWidth = LabelColWidth + DayColWidth * 7 + 40f;
        modal.style.maxHeight = Length.Percent(85f);

        // ── Title bar (draggable handle) ─────────────────────────────────────
        var titleBar = new VisualElement();
        titleBar.style.flexDirection = FlexDirection.Row;
        titleBar.style.justifyContent = Justify.SpaceBetween;
        titleBar.style.marginBottom = 8;

        var title = new Label("Shift Manager");
        title.style.fontSize = 16;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color = new StyleColor(ColTextDark);
        titleBar.Add(title);

        var closeButton = new Button(TryClose) { text = "✕" };
        closeButton.style.width = 24; closeButton.style.height = 24;
        titleBar.Add(closeButton);
        modal.Add(titleBar);

        new DraggableWindow(modal, titleBar, closeButton);

        // ── Day header row (shared across all shifts) ───────────────────────
        modal.Add(BuildDayHeaderRow());

        // ── Shifts container (one block per shift) ───────────────────────────
        shiftsContainer = new VisualElement();
        modal.Add(shiftsContainer);

        // ── Footer: Add Shift / Save ─────────────────────────────────────────
        var footer = new VisualElement();
        footer.style.flexDirection = FlexDirection.Row;
        footer.style.justifyContent = Justify.SpaceBetween;
        footer.style.marginTop = 10;

        var addButton = new Button(() => AddShift("New Shift")) { text = "+ Add Shift" };
        var saveButton = new Button(TryClose) { text = "Save & Close" };
        footer.Add(addButton);
        footer.Add(saveButton);
        modal.Add(footer);

        overlay.Add(modal);
        return overlay;
    }

    private VisualElement BuildDayHeaderRow()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;

        var corner = new VisualElement();
        corner.style.width = LabelColWidth;
        row.Add(corner);

        for (int d = 0; d < 7; d++)
        {
            var cell = new Label(DayNames[d]);
            cell.style.width = DayColWidth;
            cell.style.unityTextAlign = TextAnchor.MiddleCenter;
            cell.style.backgroundColor = new StyleColor(ColDayHeader);
            cell.style.color = new StyleColor(Color.white);
            cell.style.unityFontStyleAndWeight = FontStyle.Bold;
            cell.style.paddingTop = 4; cell.style.paddingBottom = 4;
            row.Add(cell);
        }
        return row;
    }

    // ── Per-shift block: name field + Start row + End row ───────────────────────
    private void AddShift(string defaultName)
    {
        var shift = new ShiftRow();
        for (int d = 0; d < 7; d++) { shift.Start[d] = Closed; shift.End[d] = Closed; }

        var block = new VisualElement();
        block.style.marginTop = 6;
        block.style.borderTopWidth = 1;
        block.style.borderTopColor = new StyleColor(new Color(0f, 0f, 0f, 0.15f));
        block.style.paddingTop = 4;

        // Name + remove button
        var nameRow = new VisualElement();
        nameRow.style.flexDirection = FlexDirection.Row;
        nameRow.style.alignItems = Align.Center;
        nameRow.style.marginBottom = 2;

        shift.NameField = new TextField { value = defaultName };
        shift.NameField.style.flexGrow = 1;
        shift.NameField.style.maxWidth = 200;

        var removeButton = new Button(() => RemoveShift(shift, block)) { text = "Remove" };

        nameRow.Add(shift.NameField);
        nameRow.Add(removeButton);
        block.Add(nameRow);

        block.Add(BuildTimeRow("Start", shift, isStart: true));
        block.Add(BuildTimeRow("End", shift, isStart: false));

        shift.Block = block;
        _shifts.Add(shift);
        _shiftsContainer.Add(block);
    }

    private void RemoveShift(ShiftRow shift, VisualElement block)
    {
        _shifts.Remove(shift);
        block.RemoveFromHierarchy();
    }

    private VisualElement BuildTimeRow(string rowLabel, ShiftRow shift, bool isStart)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;

        var label = new Label($"{rowLabel}:");
        label.style.width = LabelColWidth;
        label.style.backgroundColor = new StyleColor(ColLabelCell);
        label.style.color = new StyleColor(Color.white);
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.paddingLeft = 4;
        row.Add(label);

        int todayIdx = EmployeeStatSystem.GetCurrentDayOfWeek(); // Mon=0..Sun=6 — see ToSunFirst below
        int todaySunFirst = (todayIdx + 1) % 7; // convert Mon=0..Sun=6 -> Sun=0..Sat=6

        for (int d = 0; d < 7; d++)
        {
            int day = d; // capture for closures
            var dropdown = new DropdownField(TimeChoices, 0);
            dropdown.style.width = DayColWidth;
            dropdown.style.backgroundColor = new StyleColor(day == todaySunFirst ? ColTodayTint : (day % 2 == 0 ? ColCellEven : ColCellOdd));
            dropdown.SetValueWithoutNotify("Closed");

            if (isStart)
            {
                shift.StartDropdowns[day] = dropdown;
                dropdown.RegisterValueChangedCallback(evt => OnStartChanged(shift, day, evt, todaySunFirst));
            }
            else
            {
                shift.EndDropdowns[day] = dropdown;
                dropdown.RegisterValueChangedCallback(evt => OnEndChanged(shift, day, evt, todaySunFirst));
            }

            row.Add(dropdown);
        }
        return row;
    }

    private void OnStartChanged(ShiftRow shift, int day, ChangeEvent<string> evt, int todaySunFirst)
    {
        if (day == todaySunFirst && evt.newValue != evt.previousValue)
        {
            shift.StartDropdowns[day].SetValueWithoutNotify(evt.previousValue);
            UIToast.Show("Changing the same day is not allowed — schedule changes need at least 24 hours notice.", 2.5f);
            return;
        }

        shift.Start[day] = ParseTime(evt.newValue);

        // Closing the start for a day forces the end closed too — can't have an end with no start.
        if (shift.Start[day] == Closed)
        {
            shift.End[day] = Closed;
            shift.EndDropdowns[day].SetValueWithoutNotify("Closed");
        }
    }

    private void OnEndChanged(ShiftRow shift, int day, ChangeEvent<string> evt, int todaySunFirst)
    {
        if (day == todaySunFirst && evt.newValue != evt.previousValue)
        {
            shift.EndDropdowns[day].SetValueWithoutNotify(evt.previousValue);
            UIToast.Show("Changing the same day is not allowed — schedule changes need at least 24 hours notice.", 2.5f);
            return;
        }

        // Can't set an end time for a day with no start time.
        if (shift.Start[day] == Closed && evt.newValue != "Closed")
        {
            shift.EndDropdowns[day].SetValueWithoutNotify("Closed");
            UIToast.Show("Set a start time for this day before setting an end time.", 2f);
            return;
        }

        shift.End[day] = ParseTime(evt.newValue);
    }
}
