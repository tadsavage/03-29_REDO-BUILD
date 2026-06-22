// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/EmployeeRecord.cs
using System;
using UnityEngine;

// ─── Employment status ────────────────────────────────────────────────────────
public enum EmploymentStatus
{
    Active,       // Working normally
    OnLeave,      // Scheduled time off
    Injured,      // Hurt — cannot work
    Suspended,    // Disciplinary hold
    Terminated,   // Fired
    Resigned,     // Quit
    Deceased      // Gone for good
}

// ─── Work schedule ────────────────────────────────────────────────────────────
public enum WorkShift
{
    Day,          // 08:00 – 16:00
    Evening,      // 16:00 – 00:00
    Night,        // 00:00 – 08:00
    Flexible      // No fixed shift
}

[Serializable]
public class EmployeeRecord
{
    // ─── Identity ─────────────────────────────────────────────────────────────
    public string employeeGuid;
    public string employeeName;
    public string employeeId;
    public string employeeIdPrefix;
    public EmployeeGender gender;

    // ─── Role ─────────────────────────────────────────────────────────────────
    public EmployeeRole role; // = EmployeeRole.OrderSelector;

    // ─── Stats (0–100) ────────────────────────────────────────────────────────
    public float fatigue;
    public float safety;
    public float morale;
    public float skill;
    public int   skillLevel;

    // ─── Asset keys ───────────────────────────────────────────────────────────
    public string avatarResourceKey;
    public string jobIconResourceKey;

    // ─── Employment status ────────────────────────────────────────────────────
    public EmploymentStatus status;

    // ─── Salary & wages ───────────────────────────────────────────────────────
    /// <summary>Hourly wage in dollars.</summary>
    public float hourlyWage;

    /// <summary>Total wages paid out to this employee (lifetime, for accounting).</summary>
    public float totalWagesPaid;

    // ─── Dates (stored as ISO-8601 strings for JSON serialisation) ────────────
    /// <summary>Date the employee was hired. Set once at generation time.</summary>
    public string hireDateIso;

    /// <summary>Date the employee left (terminated/resigned/deceased). Empty while active.</summary>
    public string separationDateIso;

    // ─── Schedule ─────────────────────────────────────────────────────────────
    public WorkShift shift;

    /// <summary>Days the employee is scheduled to work. Bit-flags: 0=Sun,1=Mon,...,6=Sat.</summary>
    public int scheduledDaysMask;

    // ─── Injury ───────────────────────────────────────────────────────────────
    public bool  isInjured;

    /// <summary>Human-readable description of the injury (e.g. "Forklift incident").</summary>
    public string injuryDescription;

    /// <summary>Number of in-game days remaining before the employee can return to work.</summary>
    public int   injuryRecoveryDaysLeft;

    // ─── Work Schedule ────────────────────────────────────────────────────────
    /// <summary>Weekly schedule defining shifts, overtime, and availability.</summary>
    public EmployeeWorkSchedule workSchedule;

    // ─── Last-known world transform (save/restore of roaming position) ────────────
    // Captured at save time so a loaded employee resumes exactly where they were instead of
    // teleporting back to the spawn point. hasSavedPosition is false for freshly hired staff
    // and for saves made before this field existed → those spawn at the spawn point.
    public float posX, posY, posZ;
    public float rotY;
    public bool  hasSavedPosition;

    // ─── Constructor ──────────────────────────────────────────────────────────
    public EmployeeRecord()
    {
        employeeGuid = Guid.NewGuid().ToString();
        status = EmploymentStatus.Active;
        workSchedule = new EmployeeWorkSchedule();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    public bool IsAvailableForWork =>
        status == EmploymentStatus.Active && !isInjured;

    public EmployeeRecord Clone() => (EmployeeRecord)MemberwiseClone();
}
