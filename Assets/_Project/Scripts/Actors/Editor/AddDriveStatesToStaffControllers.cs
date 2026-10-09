using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Adds the MHE "driving" poses to the two shared staff controllers (MaleStaff, FemaleStaff) so an operator
/// never needs a different controller swapped in. Adds an int parameter <c>DriveStyle</c> (0 = on foot,
/// 1 = sit-drive for Dock Stocker / Reach Truck, 2 = pallet-jack walk-behind) and a Drive state per style,
/// entered from Any State and left back to Idle when DriveStyle returns to 0.
/// Safe to re-run. NOTE: Tools/Setup MaleStaff Animator clears Any State transitions, so re-run this afterwards.
/// </summary>
public static class AddDriveStatesToStaffControllers
{
    const string Dir = "Assets/_Project/Animations/AnimControllers/";
    static readonly string[] Targets =
    {
        Dir + "MaleStaff.controller",
        "Assets/_Project/Resources/Resource_AvatarSystemAssets/FemaleStaff.controller",
    };

    [MenuItem("Tools/Add Drive States To Staff Controllers")]
    public static void Run()
    {
        var dsClip = FirstMotion(Dir + "DockStocker.controller", "Drive", out float dsSpeed);
        var pjClip = FirstMotion(Dir + "PalletJack.controller", null, out float pjSpeed);
        if (dsClip == null || pjClip == null) { Debug.LogError("[AddDriveStates] Could not find the DockStocker/PalletJack drive clips."); return; }

        foreach (var path in Targets)
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (c == null) { Debug.LogError("[AddDriveStates] Missing " + path); continue; }

            bool has = false;
            foreach (var p in c.parameters) if (p.name == "DriveStyle") has = true;
            if (!has) c.AddParameter("DriveStyle", AnimatorControllerParameterType.Int);

            var sm = c.layers[0].stateMachine;
            var idle = Find(sm, "Idle");
            if (idle == null) { Debug.LogError("[AddDriveStates] No Idle state in " + path); continue; }

            Wire(sm, idle, "Drive_Sit", dsClip, dsSpeed, 1);
            Wire(sm, idle, "Drive_PalletJack", pjClip, pjSpeed, 2);
            EditorUtility.SetDirty(c);
            Debug.Log("[AddDriveStates] Updated " + path);
        }
        AssetDatabase.SaveAssets();
    }

    static void Wire(AnimatorStateMachine sm, AnimatorState idle, string name, Motion clip, float speed, int style)
    {
        var st = Find(sm, name);
        if (st == null) st = sm.AddState(name);
        st.motion = clip; st.speed = speed; st.writeDefaultValues = true;

        foreach (var t in sm.anyStateTransitions.Length > 0 ? sm.anyStateTransitions : new AnimatorStateTransition[0])
            if (t.destinationState == st) sm.RemoveAnyStateTransition(t);
        var enter = sm.AddAnyStateTransition(st);
        enter.hasExitTime = false; enter.duration = 0.2f; enter.canTransitionToSelf = false;
        enter.AddCondition(AnimatorConditionMode.Equals, style, "DriveStyle");

        foreach (var t in st.transitions) st.RemoveTransition(t);
        var leave = st.AddTransition(idle);
        leave.hasExitTime = false; leave.duration = 0.2f;
        leave.AddCondition(AnimatorConditionMode.NotEqual, style, "DriveStyle");
    }

    static AnimatorState Find(AnimatorStateMachine sm, string name)
    {
        foreach (var s in sm.states) if (s.state.name == name) return s.state;
        return null;
    }

    static Motion FirstMotion(string controllerPath, string stateName, out float speed)
    {
        speed = 1f;
        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (c == null) return null;
        foreach (var s in c.layers[0].stateMachine.states)
        {
            if (s.state.name == "Waving") continue;
            if (stateName != null && s.state.name != stateName) continue;
            speed = s.state.speed;
            return s.state.motion;
        }
        return null;
    }
}
