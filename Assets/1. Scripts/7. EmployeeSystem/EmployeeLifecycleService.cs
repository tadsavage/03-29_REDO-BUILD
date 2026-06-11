// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/EmployeeLifecycleService.cs
using System;
using UnityEngine;

/// <summary>
/// Central lifecycle authority for employee records.
/// Provides hire, fire, resign, injure, and recover operations
/// that mutate EmployeeRecord fields and broadcast events.
///
/// Attach to a single persistent GameObject (e.g. the same one that holds EmployeeRegistry).
/// </summary>
public class EmployeeLifecycleService : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static EmployeeLifecycleService Instance { get; private set; }

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
    /// <summary>An employee record was created via Hire().</summary>
    public event Action<EmployeeRecord> OnHired;

    /// <summary>An employee was fired (status set to Terminated).</summary>
    public event Action<EmployeeRecord> OnFired;

    /// <summary>An employee resigned (status set to Resigned).</summary>
    public event Action<EmployeeRecord> OnResigned;

    /// <summary>An employee was injured (isInjured=true, recovery timer set).</summary>
    public event Action<EmployeeRecord> OnInjured;

    /// <summary>An employee recovered from injury (isInjured=false, fields cleared).</summary>
    public event Action<EmployeeRecord> OnRecovered;

    /// <summary>Fired for any status or lifecycle change (general catch-all).</summary>
    public event Action<EmployeeRecord> OnStatusChanged;

    // ─── Hire ─────────────────────────────────────────────────────────────────
    /// <summary>
    /// Generate a new EmployeeRecord.
    /// Returns the record so the caller can attach it to a spawned EmployeeIdentity.
    /// </summary>
    public EmployeeRecord Hire(string idPrefix = "WHSE",
                               EmployeeGender gender = EmployeeGender.Random)
    {
        var record = EmployeeGenerator.Generate(gender, idPrefix);
        OnHired?.Invoke(record);
        OnStatusChanged?.Invoke(record);
        return record;
    }

    // ─── Fire ─────────────────────────────────────────────────────────────────
    /// <summary>Fire the employee identified by GUID.</summary>
    public void Fire(string employeeGuid)
    {
        var record = GetRecord(employeeGuid);
        if (record == null) return;
        if (record.status == EmploymentStatus.Terminated) return;

        record.status = EmploymentStatus.Terminated;
        record.separationDateIso = TodayIso();

        OnFired?.Invoke(record);
        OnStatusChanged?.Invoke(record);
    }

    // ─── Resign ───────────────────────────────────────────────────────────────
    /// <summary>The employee voluntarily resigns.</summary>
    public void Resign(string employeeGuid)
    {
        var record = GetRecord(employeeGuid);
        if (record == null) return;
        if (record.status == EmploymentStatus.Terminated ||
            record.status == EmploymentStatus.Resigned) return;

        record.status = EmploymentStatus.Resigned;
        record.separationDateIso = TodayIso();

        OnResigned?.Invoke(record);
        OnStatusChanged?.Invoke(record);
    }

    // ─── Injure ───────────────────────────────────────────────────────────────
    /// <summary>
    /// Mark an employee as injured with a description and recovery timer (in-game days).
    /// Does nothing if already injured.
    /// </summary>
    public void Injure(string employeeGuid, string description, int recoveryDays)
    {
        var record = GetRecord(employeeGuid);
        if (record == null) return;
        if (record.isInjured) return;
        if (record.status == EmploymentStatus.Terminated ||
            record.status == EmploymentStatus.Resigned ||
            record.status == EmploymentStatus.Deceased) return;

        record.isInjured              = true;
        record.injuryDescription      = description ?? string.Empty;
        record.injuryRecoveryDaysLeft = Mathf.Max(recoveryDays, 1);

        OnInjured?.Invoke(record);
        OnStatusChanged?.Invoke(record);
    }

    // ─── Recover ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Clear all injury state and set the employee back to Active.
    /// Does nothing if not currently injured.
    /// </summary>
    public void Recover(string employeeGuid)
    {
        var record = GetRecord(employeeGuid);
        if (record == null) return;
        if (!record.isInjured) return;

        record.isInjured              = false;
        record.injuryDescription      = string.Empty;
        record.injuryRecoveryDaysLeft = 0;
        record.status                 = EmploymentStatus.Active;

        OnRecovered?.Invoke(record);
        OnStatusChanged?.Invoke(record);
    }

    // ─── Change status from UI ───────────────────────────────────────────────
    /// <summary>
    /// Set employee status directly and broadcast OnStatusChanged.
    /// Used by UI panels to change status without triggering specific lifecycle events.
    /// </summary>
    public void SetStatus(EmployeeRecord record, EmploymentStatus newStatus)
    {
        if (record == null) return;
        record.status = newStatus;
        OnStatusChanged?.Invoke(record);
    }

    // ─── Convenience overloads (Identity ref instead of GUID) ─────────────────
    public void Fire   (EmployeeIdentity id) { if (id?.Record != null) Fire   (id.Record.employeeGuid); }
    public void Resign (EmployeeIdentity id) { if (id?.Record != null) Resign (id.Record.employeeGuid); }
    public void Injure (EmployeeIdentity id, string description, int recoveryDays) { if (id?.Record != null) Injure(id.Record.employeeGuid, description, recoveryDays); }
    public void Recover(EmployeeIdentity id) { if (id?.Record != null) Recover(id.Record.employeeGuid); }

    // ─── Queries ──────────────────────────────────────────────────────────────
    /// <summary>True if the employee is eligible for work/lifecycle ops.</summary>
    public bool IsActive(string employeeGuid)
    {
        var record = GetRecord(employeeGuid);
        if (record == null) return false;
        var s = record.status;
        return s == EmploymentStatus.Active
            || s == EmploymentStatus.OnLeave
            || s == EmploymentStatus.Injured
            || s == EmploymentStatus.Suspended;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    private static EmployeeRecord GetRecord(string employeeGuid)
    {
        return EmployeeRegistry.Instance?.GetByGuid(employeeGuid)?.Record;
    }

    private static string TodayIso() => DateTime.UtcNow.ToString("yyyy-MM-dd");
}
