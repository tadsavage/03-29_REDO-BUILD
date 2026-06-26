// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/EmployeeStatSystem.cs
using System;
using UnityEngine;

/// <summary>
/// Daily tick service for employee stat changes.
/// Handles:
/// - Fatigue decay when working, replenish when resting
/// - Skill progression (only while working and not on leave)
/// - Leave status recovery (no experience gain while on leave)
/// - Overtime tracking (prevents fatigue replenish off-shift)
///
/// Must be called once per in-game day (or per game tick, depending on your game loop).
/// </summary>
public class EmployeeStatSystem : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static EmployeeStatSystem Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ─── Configuration (Inspector-adjustable) ─────────────────────────────────
    /// <summary>Fatigue gained per work day (0–100 scale).</summary>
    [SerializeField]
    private float _fatiguePerWorkDay = 15f;

    /// <summary>Overtime fatigue gain is _fatiguePerWorkDay multiplied by this (2x = doubled,
    /// per the "working overtime doubles fatigue decay" rule). Replaces an earlier independent
    /// flat overtime constant so the "doubled" relationship is exact and stays exact if
    /// _fatiguePerWorkDay is retuned.</summary>
    [SerializeField]
    private float _overtimeFatigueMultiplier = 2f;

    /// <summary>Fatigue replenished per rest day (0–100 scale).</summary>
    [SerializeField]
    private float _fatigueReplenishPerRestDay = 20f;

    /// <summary>Skill points gained per work day (when not on leave).</summary>
    [SerializeField]
    private float _skillPointsPerWorkDay = 0.5f;

    /// <summary>Skill points gained per overtime shift (bonus).</summary>
    [SerializeField]
    private float _skillPointsPerOvertimeDay = 0.3f;

    /// <summary>Morale penalty per work day (can be positive for morale gain).</summary>
    [SerializeField]
    private float _moralePerWorkDay = -1f;

    /// <summary>Safety improvement per work day (skill-based).</summary>
    [SerializeField]
    private float _safetyPerWorkDay = 0.2f;

    // ─── Events ───────────────────────────────────────────────────────────────
    /// <summary>Daily stat tick completed for an employee.</summary>
    public event Action<EmployeeRecord> OnStatsTicked;

    /// <summary>Employee skill increased (data: skill points gained).</summary>
    public event Action<EmployeeRecord, float> OnSkillIncreased;

    /// <summary>Employee fatigue changed critically (near 100 or near 0).</summary>
    public event Action<EmployeeRecord> OnFatigueAlert;

    // ─── Public API ───────────────────────────────────────────────────────────
    /// <summary>
    /// Process one day's stat changes for an employee.
    /// Call this once per in-game day for each active employee.
    /// </summary>
    public void TickDay(EmployeeRecord record)
    {
        if (record == null) return;

        // Employees on leave do NOT gain experience, but DO recover fatigue
        bool isOnLeave = record.status == EmploymentStatus.OnLeave;

        // Employees that are not working recover fatigue (unless on overtime)
        bool isWorkingToday = IsEmployeeWorkingToday(record);
        bool isOvertimeToday = IsEmployeeOvertimeToday(record);

        // ─── Fatigue ──────────────────────────────────────────────────────────
        if (isWorkingToday)
        {
            // Lose fatigue when working
            float fatigueGain = isOvertimeToday ? _fatiguePerWorkDay * _overtimeFatigueMultiplier : _fatiguePerWorkDay;
            record.fatigue = Mathf.Clamp(record.fatigue + fatigueGain, 0f, 100f);
        }
        else if (!isOvertimeToday)
        {
            // Replenish fatigue when resting and not on overtime
            record.fatigue = Mathf.Clamp(record.fatigue - _fatigueReplenishPerRestDay, 0f, 100f);
        }

        // ─── Skill & Experience (NOT on leave) ────────────────────────────────
        if (!isOnLeave && isWorkingToday)
        {
            float skillGain = isOvertimeToday
                ? _skillPointsPerWorkDay + _skillPointsPerOvertimeDay
                : _skillPointsPerWorkDay;

            record.skill = Mathf.Clamp(record.skill + skillGain, 0f, 100f);

            // Level up if threshold crossed
            int skillLevelThreshold = 20 + (record.skillLevel * 15); // 20, 35, 50, 65, 80...
            if (record.skill >= skillLevelThreshold && record.skillLevel < 5)
            {
                record.skillLevel++;
            }

            OnSkillIncreased?.Invoke(record, skillGain);
        }

        // ─── Morale ───────────────────────────────────────────────────────────
        if (isWorkingToday)
        {
            // Work decreases morale slightly (or increases if value is positive)
            record.morale = Mathf.Clamp(record.morale + _moralePerWorkDay, 0f, 100f);
        }
        else
        {
            // Rest improves morale
            record.morale = Mathf.Clamp(record.morale + Mathf.Abs(_moralePerWorkDay), 0f, 100f);
        }

        // ─── Safety ───────────────────────────────────────────────────────────
        if (isWorkingToday)
        {
            // Safety improves with skill-based training
            float safetyGain = _safetyPerWorkDay * (record.skill / 100f);
            record.safety = Mathf.Clamp(record.safety + safetyGain, 0f, 100f);
        }

        // ─── Injury recovery ──────────────────────────────────────────────────
        if (record.isInjured && record.injuryRecoveryDaysLeft > 0)
        {
            record.injuryRecoveryDaysLeft--;
            if (record.injuryRecoveryDaysLeft <= 0)
            {
                record.isInjured = false;
                record.injuryDescription = string.Empty;
            }
        }

        OnStatsTicked?.Invoke(record);

        // Alert if fatigue is critical
        if (record.fatigue >= 95f || record.fatigue <= 5f)
        {
            OnFatigueAlert?.Invoke(record);
        }
    }

    /// <summary>
    /// Process one day for all employees in the registry.
    /// Call this once per in-game day (e.g., from a game time manager).
    /// </summary>
    public void TickDayForAll()
    {
        var registry = EmployeeRegistry.Instance;
        if (registry == null) return;

        foreach (var identity in registry.All)
        {
            if (identity?.Record != null)
            {
                TickDay(identity.Record);
            }
        }
    }

    // ─── Schedule checks ──────────────────────────────────────────────────────
    /// <summary>
    /// Check if the employee is scheduled to work today.
    /// Uses the current day of week and their work schedule.
    /// </summary>
    private bool IsEmployeeWorkingToday(EmployeeRecord record)
    {
        if (record == null || record.workSchedule == null)
            return false;

        // Skip if not active/available
        if (record.status != EmploymentStatus.Active && record.status != EmploymentStatus.OnLeave)
            return false;

        int dayOfWeek = GetCurrentDayOfWeek();
        return record.workSchedule.IsScheduledForDay(dayOfWeek);
    }

    /// <summary>
    /// Check if the employee is working overtime today.
    /// </summary>
    private bool IsEmployeeOvertimeToday(EmployeeRecord record)
    {
        if (record == null || record.workSchedule == null)
            return false;

        int dayOfWeek = GetCurrentDayOfWeek();
        return record.workSchedule.IsOvertimeDay(dayOfWeek);
    }

    /// <summary>
    /// Get the current day of the week (0 = Monday, 6 = Sunday).
    /// Placeholder for your actual game time system — uses real-world time, NOT in-game day, so
    /// this drifts from SimulationTimeService.Day. Public/static so EmployeeOvertimeService can
    /// share the exact same (currently wrong) notion of "today" rather than duplicating it.
    /// </summary>
    public static int GetCurrentDayOfWeek()
    {
        // TODO: Replace with your actual game time system
        // For now, use real-world day of week as placeholder
        int realDay = (int)System.DateTime.Now.DayOfWeek;
        // Convert: Sunday (0) → Sunday (6), Monday (1) → Monday (0), etc.
        return realDay == 0 ? 6 : realDay - 1;
    }

    // ─── Accessors for tuning ─────────────────────────────────────────────────
    public float GetFatiguePerWorkDay() => _fatiguePerWorkDay;
    public void SetFatiguePerWorkDay(float value) => _fatiguePerWorkDay = value;

    public float GetOvertimeFatigueMultiplier() => _overtimeFatigueMultiplier;
    public void SetOvertimeFatigueMultiplier(float value) => _overtimeFatigueMultiplier = value;

    public float GetFatigueReplenishPerRestDay() => _fatigueReplenishPerRestDay;
    public void SetFatigueReplenishPerRestDay(float value) => _fatigueReplenishPerRestDay = value;

    public float GetSkillPointsPerWorkDay() => _skillPointsPerWorkDay;
    public void SetSkillPointsPerWorkDay(float value) => _skillPointsPerWorkDay = value;

    // ─── Editor Debug ─────────────────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("Tick Day for All Employees")]
    private void EditorTickDayForAll()
    {
        TickDayForAll();
        Debug.Log("[EmployeeStatSystem] Daily tick completed for all employees.");
    }
#endif
}
