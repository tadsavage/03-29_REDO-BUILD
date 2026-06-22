// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/EmployeeScheduleManager.cs
using UnityEngine;

/// <summary>
/// Framework for employee schedule management.
/// 
/// Provides centralized access to employee schedules with hooks for UI-driven edits.
/// The actual UI for creating/editing schedules is NOT implemented yet—just the framework.
/// 
/// Future UI will call SetSchedule() to persist changes.
/// </summary>
public class EmployeeScheduleManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static EmployeeScheduleManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ─── Events ───────────────────────────────────────────────────────────────
    /// <summary>Fired when an employee's schedule is updated.</summary>
    public event System.Action<EmployeeRecord> OnScheduleChanged;

    // ─── Public API ───────────────────────────────────────────────────────────
    /// <summary>Get the current schedule for an employee (by GUID).</summary>
    public EmployeeWorkSchedule GetSchedule(string employeeGuid)
    {
        var record = GetRecord(employeeGuid);
        return record?.workSchedule;
    }

    /// <summary>
    /// Update the schedule for an employee.
    /// Called from UI layer when user edits shifts/overtime.
    /// </summary>
    public void SetSchedule(string employeeGuid, EmployeeWorkSchedule newSchedule)
    {
        var record = GetRecord(employeeGuid);
        if (record == null || newSchedule == null) return;

        record.workSchedule = newSchedule.Clone();
        OnScheduleChanged?.Invoke(record);
    }

    /// <summary>
    /// Convenience overload: set schedule via EmployeeIdentity.
    /// </summary>
    public void SetSchedule(EmployeeIdentity identity, EmployeeWorkSchedule newSchedule)
    {
        if (identity?.Record != null)
        {
            SetSchedule(identity.Record.employeeGuid, newSchedule);
        }
    }

    /// <summary>
    /// Set a specific shift for a specific day.
    /// Called from UI when user clicks on a day cell and picks a shift.
    /// </summary>
    public void SetShift(string employeeGuid, int dayOfWeek, WorkShift shift)
    {
        var record = GetRecord(employeeGuid);
        if (record?.workSchedule == null) return;

        record.workSchedule.SetShift(dayOfWeek, shift);
        OnScheduleChanged?.Invoke(record);
    }

    /// <summary>
    /// Toggle overtime flag for a specific day.
    /// Called from UI when user marks day as overtime/not-overtime.
    /// </summary>
    public void SetOvertime(string employeeGuid, int dayOfWeek, bool isOvertime)
    {
        var record = GetRecord(employeeGuid);
        if (record?.workSchedule == null) return;

        record.workSchedule.SetOvertime(dayOfWeek, isOvertime);
        OnScheduleChanged?.Invoke(record);
    }

    /// <summary>
    /// Get shift for a specific day.
    /// </summary>
    public WorkShift GetShift(string employeeGuid, int dayOfWeek)
    {
        var schedule = GetSchedule(employeeGuid);
        return schedule?.GetShift(dayOfWeek) ?? WorkShift.Flexible;
    }

    /// <summary>
    /// Check if a specific day is marked as overtime.
    /// </summary>
    public bool IsOvertimeDay(string employeeGuid, int dayOfWeek)
    {
        var schedule = GetSchedule(employeeGuid);
        return schedule?.IsOvertimeDay(dayOfWeek) ?? false;
    }

    /// <summary>
    /// Set all shifts for the week at once (utility for templates).
    /// Helpful for applying pre-made schedule templates like "Standard Mon-Fri Day Shift".
    /// </summary>
    public void SetWeeklySchedule(string employeeGuid, WorkShift[] shiftsPerDay)
    {
        var record = GetRecord(employeeGuid);
        if (record?.workSchedule == null || shiftsPerDay == null || shiftsPerDay.Length != 7) return;

        for (int i = 0; i < 7; i++)
        {
            record.workSchedule.SetShift(i, shiftsPerDay[i]);
        }

        OnScheduleChanged?.Invoke(record);
    }

    // ─── Debug / Templates (for testing) ───────────────────────────────────────
    #if UNITY_EDITOR
    /// <summary>
    /// Apply a "Mon-Fri Day Shift" template to an employee.
    /// Useful for testing; in production, this would come from UI.
    /// </summary>
    public void ApplyTemplate_StandardMonFriDay(string employeeGuid)
    {
        var shifts = new WorkShift[7]
        {
            WorkShift.Day,
            WorkShift.Day,
            WorkShift.Day,
            WorkShift.Day,
            WorkShift.Day,
            WorkShift.Flexible,
            WorkShift.Flexible
        };
        SetWeeklySchedule(employeeGuid, shifts);
    }

    /// <summary>
    /// Apply a "7-day shift rotation" template (useful for 24h operations).
    /// </summary>
    public void ApplyTemplate_SevenDayRotation(string employeeGuid)
    {
        var shifts = new WorkShift[7]
        {
            WorkShift.Day,
            WorkShift.Day,
            WorkShift.Evening,
            WorkShift.Evening,
            WorkShift.Night,
            WorkShift.Night,
            WorkShift.Flexible
        };
        SetWeeklySchedule(employeeGuid, shifts);
    }

    [ContextMenu("Apply Standard Template to All")]
    private void EditorApplyTemplateAll()
    {
        var registry = EmployeeRegistry.Instance;
        if (registry == null) return;

        foreach (var identity in registry.All)
        {
            if (identity?.Record != null)
            {
                ApplyTemplate_StandardMonFriDay(identity.Record.employeeGuid);
            }
        }
        Debug.Log("[EmployeeScheduleManager] Applied standard template to all employees.");
    }
    #endif

    // ─── Helpers ──────────────────────────────────────────────────────────────
    private static EmployeeRecord GetRecord(string employeeGuid)
    {
        return EmployeeRegistry.Instance?.GetByGuid(employeeGuid)?.Record;
    }
}
