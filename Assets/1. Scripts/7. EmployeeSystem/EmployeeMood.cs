using UnityEngine;

/// <summary>
/// Coarse emotional state derived from an employee's stats. Drives mood-based Animator
/// behaviour wherever an employee model is rendered — the photo booth live feed today,
/// and potentially in-world idle behaviour later (see EmployeeMoodAnimator).
/// </summary>
public enum EmployeeMood
{
    Neutral,
    Happy,
    Tired,
    Angry
}

/// <summary>
/// Maps EmployeeRecord stats to EmployeeMood. Posture and gesture are evaluated
/// independently — a tired-but-happy employee slouches AND still waves; fatigue no
/// longer silently swallows a high-morale gesture.
/// </summary>
public static class EmployeeMoodEvaluator
{
    private const float HappyMoraleThreshold = 75f;
    private const float AngryMoraleThreshold = 25f;
    private const float TiredFatigueThreshold = 75f;

    /// <summary>Persistent held pose (e.g. tired slouch) — driven purely by fatigue.</summary>
    public static EmployeeMood EvaluatePosture(EmployeeRecord record)
    {
        if (record == null) return EmployeeMood.Neutral;
        if (record.fatigue >= TiredFatigueThreshold) return EmployeeMood.Tired;
        return EmployeeMood.Neutral;
    }

    /// <summary>Periodic gesture (wave / rude gesture) — driven purely by morale, regardless of fatigue.</summary>
    public static EmployeeMood EvaluateGesture(EmployeeRecord record)
    {
        if (record == null) return EmployeeMood.Neutral;
        if (record.morale <= AngryMoraleThreshold) return EmployeeMood.Angry;
        if (record.morale >= HappyMoraleThreshold) return EmployeeMood.Happy;
        return EmployeeMood.Neutral;
    }
}

/// <summary>
/// Applies an EmployeeMood to an Animator via bool parameters. Two kinds of param:
///   - "Persistent" — held for as long as the mood is active (e.g. IsTired slouch pose).
///   - "Gesture"    — pulsed periodically while the mood is active (e.g. IsWaving, IsAngry).
///
/// IsTired and IsAngry don't exist on MaleStaff.controller yet — adding them (plus the
/// states/clips they should trigger) is what makes these calls visible in-game.
/// </summary>
public static class EmployeeMoodAnimator
{
    public const string TiredParam = "IsTired";
    public const string AngryParam = "IsAngry";
    public const string WavingParam = "IsWaving";

    /// <summary>Bool param that should pulse periodically for this mood's "gesture", or null if this mood has no periodic gesture.</summary>
    public static string GestureParam(EmployeeMood mood) => mood switch
    {
        EmployeeMood.Happy => WavingParam,
        EmployeeMood.Angry => AngryParam,
        _ => null
    };

    /// <summary>Sets the held bool params for a mood. Call once whenever the mood is (re)evaluated.</summary>
    public static void ApplyPersistent(Animator animator, EmployeeMood mood)
    {
        if (animator == null) return;
        animator.SetBool(TiredParam, mood == EmployeeMood.Tired);
    }
}
