using UnityEngine;

/// <summary>
/// Player-initiated overtime/early-dismissal actions, driven by the Actions dropdown
/// (EmployeeInfoUI). Mirrors EmployeeAssignmentService/EmployeeTerminationService's role as a
/// static, no-Inspector-wiring shared service.
///
/// "Ask to Work OT" marks today as an overtime day on the employee's EmployeeWorkSchedule
/// (EmployeeWorkSchedule.SetOvertime) — this is what EmployeeStatSystem.TickDay reads to double
/// fatigue gain for the day (see EmployeeStatSystem._overtimeFatigueMultiplier). NOTE:
/// EmployeeStatSystem's daily tick is NOT currently wired to fire automatically anywhere in the
/// game — it only runs from its own Editor debug context menu — so the fatigue-doubling effect
/// won't be visible during normal play until that's wired to a real day-change event. Flagged
/// to Tad rather than silently fixed, since it's a separate pre-existing gap.
///
/// Both actions apply their morale penalty immediately and unconditionally — actual overtime
/// PAY (1.5x) already happens automatically by hour via PayrollService/ShiftSchedule regardless
/// of whether the player used "Ask to Work OT"; that button is the morale-cost flavor action of
/// formally asking, not a gate on whether overtime pay/fatigue occurs.
/// </summary>
public static class EmployeeOvertimeService
{
    private const float LowMoraleThreshold = 40f;
    private const float OvertimeAskMoralePenalty = 5f;
    private const float LowMoraleOvertimeAskMoralePenalty = 12f;

    // TODO: scale by difficulty once a global Difficulty class exists — Tad's note, 2026-06-25.
    private const float SendHomeMoralePenalty = 10f;

    public static void AskToWorkOvertime(EmployeeIdentity identity)
    {
        var record = identity?.Record;
        if (record == null) return;

        record.workSchedule ??= new EmployeeWorkSchedule();
        record.workSchedule.SetOvertime(EmployeeStatSystem.GetCurrentDayOfWeek(), true);

        float penalty = record.morale < LowMoraleThreshold
            ? LowMoraleOvertimeAskMoralePenalty
            : OvertimeAskMoralePenalty;
        record.morale = Mathf.Clamp(record.morale - penalty, 0f, 100f);

        UIToast.Show($"{record.employeeName} agrees to work overtime.", 2f);
    }

    public static void SendHome(EmployeeIdentity identity)
    {
        var record = identity?.Record;
        if (record == null) return;

        record.morale = Mathf.Clamp(record.morale - SendHomeMoralePenalty, 0f, 100f);

        UIToast.Show($"{record.employeeName} sent home early.", 2f);
    }
}
