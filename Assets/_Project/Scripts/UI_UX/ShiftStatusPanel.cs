using GameCore.Economy;
using UnityEngine.UIElements;
using static FinanceUIKit;

/// Shift status — triggered by the "Time" label in TopBarUI. Shows hours left in whichever
/// shift window currently contains the in-game time, and how many active employees are
/// currently working past their own scheduled shift (paid overtime — see PayrollService).
public class ShiftStatusPanel : ITopBarPanel
{
    readonly VisualElement _panel;
    readonly SimulationTimeService _time;
    Label _hoursLeftLabel;
    Label _overtimeCountLabel;
    bool _visible;

    public ShiftStatusPanel(VisualElement root, SimulationTimeService time)
    {
        _time = time;
        _panel = Build();
        _panel.style.display = DisplayStyle.None;
        root.Add(_panel);
        Refresh();
    }

    public bool IsVisible => _visible;
    public VisualElement Root => _panel;
    public void Toggle() { if (_visible) Hide(); else Show(); }

    public void Show()
    {
        _visible = true;
        _panel.style.display = DisplayStyle.Flex;
        Refresh();
    }

    public void Hide()
    {
        _visible = false;
        _panel.style.display = DisplayStyle.None;
    }

    /// <summary>Called by TopBarUI's own per-minute refresh — only does work while open.</summary>
    public void RefreshIfVisible()
    {
        if (_visible) Refresh();
    }

    public void Dispose()
    {
        if (_panel.parent != null) _panel.RemoveFromHierarchy();
    }

    VisualElement Build()
    {
        var panel = Panel();
        panel.Add(SectionHeader("Shift Status", ColBlueDark, ColBlueTint));

        _hoursLeftLabel = ValueLabel(bold: true, color: ColOrange);
        panel.Add(DataRow("Hours Left in Shift", _hoursLeftLabel, ColRowA, false));

        _overtimeCountLabel = ValueLabel(bold: true, color: ColOrange);
        panel.Add(DataRow("Employees on Overtime", _overtimeCountLabel, ColRowB, false));

        return panel;
    }

    void Refresh()
    {
        if (_time == null) return;

        int hour = _time.Hour, minute = _time.Minute;
        string shiftLabel = ShiftSchedule.CurrentShiftLabel(hour);
        float hoursLeft = ShiftSchedule.HoursLeftInCurrentShift(hour, minute);

        _hoursLeftLabel.text = $"{hoursLeft:0.0} ({shiftLabel})";

        int overtimeCount = 0;
        if (EmployeeRegistry.Instance != null)
        {
            foreach (var id in EmployeeRegistry.Instance.All)
            {
                if (id == null || id.SystemManaged || id.Record == null) continue;
                if (id.Record.status != EmploymentStatus.Active) continue;
                if (ShiftSchedule.IsOvertime(id.Record.shift, hour)) overtimeCount++;
            }
        }
        _overtimeCountLabel.text = overtimeCount.ToString();
    }
}
