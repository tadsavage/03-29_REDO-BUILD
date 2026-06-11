// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/EmployeeWorkSchedule.cs
using System;
using UnityEngine;

/// <summary>
/// Defines a single week of work assignments for an employee.
/// Contains shift assignments, overtime flags, and notes.
/// 
/// Can be a ScriptableObject (reusable template) or serialized directly in EmployeeRecord.
/// For now: created/edited via code; UI framework left for future.
/// </summary>
[Serializable]
public class EmployeeWorkSchedule
{
    // ─── Week layout (Monday-Sunday, indices 0–6) ─────────────────────────────
    /// <summary>
    /// Shift assignment for each day of the week.
    /// Index 0 = Monday, 6 = Sunday.
    /// WorkShift.Flexible = not scheduled / placeholder.
    /// </summary>
    [SerializeField]
    public WorkShift[] shiftsPerDay = new WorkShift[7];

    // ─── Overtime tracking ────────────────────────────────────────────────────
    /// <summary>
    /// Days per week the employee is working overtime (extra hours/shifts).
    /// Bit-flags: bit 0 = Monday, bit 6 = Sunday.
    /// Employee working overtime will NOT replenish fatigue off-shift.
    /// </summary>
    [SerializeField]
    public int overtimeDaysMask;

    // ─── Metadata ─────────────────────────────────────────────────────────────
    /// <summary>Human-readable notes (e.g. "Temporary assignment", "Trial shift", etc.)</summary>
    [SerializeField]
    public string notes = string.Empty;

    /// <summary>Date this schedule became active (ISO-8601).</summary>
    [SerializeField]
    public string effectiveDateIso = string.Empty;

    // ─── Constructor ──────────────────────────────────────────────────────────
    public EmployeeWorkSchedule()
    {
        // Default: all days unscheduled
        for (int i = 0; i < 7; i++)
            shiftsPerDay[i] = WorkShift.Flexible;
        overtimeDaysMask = 0;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    /// <summary>Get the shift for a given day (0=Mon, 6=Sun).</summary>
    public WorkShift GetShift(int dayOfWeek)
    {
        if (dayOfWeek < 0 || dayOfWeek >= 7) return WorkShift.Flexible;
        return shiftsPerDay[dayOfWeek];
    }

    /// <summary>Set the shift for a given day (0=Mon, 6=Sun).</summary>
    public void SetShift(int dayOfWeek, WorkShift shift)
    {
        if (dayOfWeek < 0 || dayOfWeek >= 7) return;
        shiftsPerDay[dayOfWeek] = shift;
    }

    /// <summary>True if the employee is scheduled for work on this day (shift != Flexible).</summary>
    public bool IsScheduledForDay(int dayOfWeek)
    {
        return GetShift(dayOfWeek) != WorkShift.Flexible;
    }

    /// <summary>True if the employee is working overtime on this day.</summary>
    public bool IsOvertimeDay(int dayOfWeek)
    {
        if (dayOfWeek < 0 || dayOfWeek >= 7) return false;
        return (overtimeDaysMask & (1 << dayOfWeek)) != 0;
    }

    /// <summary>Set overtime flag for a specific day.</summary>
    public void SetOvertime(int dayOfWeek, bool isOvertime)
    {
        if (dayOfWeek < 0 || dayOfWeek >= 7) return;
        if (isOvertime)
            overtimeDaysMask |= (1 << dayOfWeek);
        else
            overtimeDaysMask &= ~(1 << dayOfWeek);
    }

    /// <summary>Total days scheduled this week (excludes Flexible shifts).</summary>
    public int GetScheduledDaysCount()
    {
        int count = 0;
        for (int i = 0; i < 7; i++)
        {
            if (shiftsPerDay[i] != WorkShift.Flexible)
                count++;
        }
        return count;
    }

    /// <summary>Total days with overtime this week.</summary>
    public int GetOvertimeDaysCount()
    {
        int count = 0;
        for (int i = 0; i < 7; i++)
        {
            if (IsOvertimeDay(i))
                count++;
        }
        return count;
    }

    /// <summary>Create a copy of this schedule.</summary>
    public EmployeeWorkSchedule Clone()
    {
        var clone = new EmployeeWorkSchedule
        {
            shiftsPerDay = (WorkShift[])shiftsPerDay.Clone(),
            overtimeDaysMask = overtimeDaysMask,
            notes = notes,
            effectiveDateIso = effectiveDateIso
        };
        return clone;
    }
}
