/// <summary>
/// Shift-window logic shared by PayrollService (overtime pay) and ShiftStatusPanel (the "Time"
/// dropdown's hours-left / overtime-count display). Single source of truth for what counts as
/// "in shift" vs "overtime" for a given WorkShift at a given hour.
/// </summary>
public static class ShiftSchedule
{
    /// <summary>Shift window as [start, end) in 24h hours. Evening wraps past midnight (16-24);
    /// Flexible has no fixed window — callers should treat it as always in-shift, never overtime.</summary>
    public static (int start, int end) WindowFor(WorkShift shift) => shift switch
    {
        WorkShift.Day     => (8, 16),
        WorkShift.Evening => (16, 24),
        WorkShift.Night   => (0, 8),
        _                 => (0, 24), // Flexible
    };

    public static bool IsWithinShift(WorkShift shift, int hour)
    {
        if (shift == WorkShift.Flexible) return true;
        var (start, end) = WindowFor(shift);
        return hour >= start && hour < end;
    }

    /// <summary>True if an employee on this shift, at this hour, is working past their scheduled
    /// stop — i.e. owed overtime pay. Flexible employees have no fixed shift and are never
    /// considered overtime.</summary>
    public static bool IsOvertime(WorkShift shift, int hour) =>
        shift != WorkShift.Flexible && !IsWithinShift(shift, hour);

    /// <summary>Human-readable label for whichever shift's window currently contains this hour —
    /// e.g. "Day Shift" at 14:00, "Evening Shift" at 19:00, "Night Shift" at 02:00.</summary>
    public static string CurrentShiftLabel(int hour)
    {
        if (IsWithinShift(WorkShift.Day, hour))     return "Day Shift";
        if (IsWithinShift(WorkShift.Evening, hour)) return "Evening Shift";
        return "Night Shift";
    }

    /// <summary>Hours remaining (fractional) until the shift whose window currently contains
    /// `hour` ends. Used for the "Hours left in [X] Shift" status line.</summary>
    public static float HoursLeftInCurrentShift(int hour, int minute)
    {
        WorkShift current = IsWithinShift(WorkShift.Day, hour)     ? WorkShift.Day
                           : IsWithinShift(WorkShift.Evening, hour) ? WorkShift.Evening
                           :                                          WorkShift.Night;

        var (_, end) = WindowFor(current);
        float nowFrac = hour + minute / 60f;
        float endFrac = end; // Night's end (8) and Day's end (16) are same-day; Evening's end (24) too
        float remaining = endFrac - nowFrac;
        if (remaining < 0f) remaining += 24f;
        return remaining;
    }
}
