using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Run once via Tools → Setup MaleStaff Animator to rebuild all MaleStaff
/// controller transitions from scratch with correct, professional settings.
///
/// Safe to re-run any time — it clears and re-adds every transition.
/// </summary>
public static class SetupMaleStaffAnimator
{
    const string ControllerPath = "Assets/_Project/Animations/AnimControllers/MaleStaff.controller";

    [MenuItem("Tools/Setup MaleStaff Animator")]
    public static void Run()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[SetupMaleStaffAnimator] Controller not found at: {ControllerPath}");
            return;
        }

        Undo.RecordObject(controller, "Setup MaleStaff Animator");

        var sm = controller.layers[0].stateMachine;

        // ── Locate states ─────────────────────────────────────────────────────
        var idle        = FindState(sm, "Idle");
        var walking     = FindState(sm, "Walking");
        var turnLeft    = FindState(sm, "simpleLeft");
        var turnRight   = FindState(sm, "simpleRight");
        var waving      = FindState(sm, "Waving");
        var climbing    = FindState(sm, "Climbing");
        var jumpingDown = FindState(sm, "JumpingDown");

        if (idle == null || walking == null || turnLeft == null || turnRight == null || waving == null)
        {
            Debug.LogError("[SetupMaleStaffAnimator] One or more required states are missing. Aborting.");
            return;
        }

        // ── Clear all existing transitions ────────────────────────────────────
        AnimatorState[] states = { idle, walking, turnLeft, turnRight, waving, climbing, jumpingDown };
        foreach (var s in states)
            if (s != null) ClearTransitions(s);
        ClearAnyStateTransitions(sm);

        // ── State speeds ──────────────────────────────────────────────────────
        // Turn animations play at 2× to better match locomotion pace.
        // Tune this in the Inspector on the MaleStaff controller if needed.
        turnLeft.speed  = 2f;
        turnRight.speed = 2f;

        // ── Rebuild transitions ───────────────────────────────────────────────

        // Idle → Walking: fire immediately when IsWalking goes true
        Tr(idle, walking, "IsWalking", true,  hasExitTime: false, dur: 0.15f);
        // Idle → Waving: fire immediately when IsWaving goes true
        Tr(idle, waving,  "IsWaving",  true,  hasExitTime: false, dur: 0.2f);

        // Walking → Idle: fire immediately when IsWalking goes false (no delay, no sliding)
        Tr(walking, idle,      "IsWalking",      false, hasExitTime: false, dur: 0.1f);
        // Walking → TurnLeft/Right: fire immediately when turn bool fires
        Tr(walking, turnLeft,  "IsTurningLeft",  true,  hasExitTime: false, dur: 0.08f);
        Tr(walking, turnRight, "IsTurningRight", true,  hasExitTime: false, dur: 0.08f);

        // TurnLeft → Walking: wait for 85% of animation, then exit when no longer turning.
        // This ensures the full turn plays through before returning to walk stride.
        TrExitTime(turnLeft, walking, "IsTurningLeft",  false, exitTime: 0.85f, dur: 0.08f);
        // TurnLeft → TurnRight: immediate direction reversal mid-turn
        Tr(turnLeft, turnRight, "IsTurningRight", true, hasExitTime: false, dur: 0.05f);

        // TurnRight → Walking: same exit-time pattern for symmetry
        TrExitTime(turnRight, walking, "IsTurningRight", false, exitTime: 0.85f, dur: 0.08f);
        // TurnRight → TurnLeft: immediate direction reversal mid-turn
        Tr(turnRight, turnLeft, "IsTurningLeft", true, hasExitTime: false, dur: 0.05f);

        // Waving → Idle: return to idle as soon as IsWaving goes false
        Tr(waving, idle, "IsWaving", false, hasExitTime: false, dur: 0.25f);

        // Climbing → Idle and JumpingDown → Idle
        if (climbing    != null) Tr(climbing,    idle, "IsClimbing",    false, hasExitTime: false, dur: 0.15f);
        if (jumpingDown != null) Tr(jumpingDown, idle, "IsJumpingDown", false, hasExitTime: false, dur: 0.15f);

        // AnyState → Climbing / JumpingDown: interrupt any other state when the ledge
        // traversal begins. CanTransitionToSelf=false prevents re-triggering while
        // already in the climbing/jumping state.
        if (climbing    != null) TrAny(sm, climbing,    "IsClimbing",    true, dur: 0.1f);
        if (jumpingDown != null) TrAny(sm, jumpingDown, "IsJumpingDown", true, dur: 0.1f);

        // ── Save ──────────────────────────────────────────────────────────────
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log("[SetupMaleStaffAnimator] All transitions rebuilt successfully.");
        EditorUtility.DisplayDialog(
            "MaleStaff Animator",
            "All transitions rebuilt.\n\n" +
            "• Walk ↔ Idle:    instant (no exit-time delay)\n" +
            "• Walk → Turn:    instant when angle exceeds threshold\n" +
            "• Turn → Walk:    after 85% of turn animation plays\n" +
            "• Turn direction reversal:  immediate\n" +
            "• Turn speed:     2× (adjust on state in controller)\n" +
            "• Wave / Climb / Jump:  fully wired",
            "OK");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// Transition that fires immediately when the condition is met (no exit time).
    static void Tr(AnimatorState from, AnimatorState to, string param, bool isTrue, bool hasExitTime, float dur)
    {
        var t = from.AddTransition(to);
        t.hasExitTime         = hasExitTime;
        t.exitTime            = 0f;
        t.duration            = dur;
        t.hasFixedDuration    = true;
        t.canTransitionToSelf = false;
        t.AddCondition(isTrue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
    }

    /// Transition that waits for exitTime% of the animation, then fires if condition is met.
    static void TrExitTime(AnimatorState from, AnimatorState to, string param, bool isTrue, float exitTime, float dur)
    {
        var t = from.AddTransition(to);
        t.hasExitTime         = true;
        t.exitTime            = exitTime;
        t.duration            = dur;
        t.hasFixedDuration    = true;
        t.canTransitionToSelf = false;
        t.AddCondition(isTrue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
    }

    /// AnyState transition — interrupts any currently playing state.
    static void TrAny(AnimatorStateMachine sm, AnimatorState to, string param, bool isTrue, float dur)
    {
        var t = sm.AddAnyStateTransition(to);
        t.hasExitTime         = false;
        t.exitTime            = 0f;
        t.duration            = dur;
        t.hasFixedDuration    = true;
        t.canTransitionToSelf = false;
        t.AddCondition(isTrue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
    }

    static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        foreach (var cs in sm.states)
            if (cs.state.name == name) return cs.state;
        Debug.LogError($"[SetupMaleStaffAnimator] State '{name}' not found in {sm.name}.");
        return null;
    }

    static void ClearTransitions(AnimatorState state)
    {
        // AnimatorStateMachine has no RemoveTransition API — use SerializedObject
        // to directly zero out the state's m_Transitions array.
        var so = new SerializedObject(state);
        so.Update();
        so.FindProperty("m_Transitions").ClearArray();
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void ClearAnyStateTransitions(AnimatorStateMachine sm)
    {
        var so = new SerializedObject(sm);
        so.Update();
        so.FindProperty("m_AnyStateTransitions").ClearArray();
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
