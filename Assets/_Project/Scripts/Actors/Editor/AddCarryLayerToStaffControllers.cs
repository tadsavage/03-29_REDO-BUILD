using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Adds the "carrying a case" arm pose to the two shared staff controllers (MaleStaff, FemaleStaff).
/// There was no carry animation in the project, so the pose is AUTHORED here as a humanoid muscle clip
/// (both arms forward, elbows bent, hands either side of a case at ~1.16 m, tuned by rendering the
/// Male_Regular_Body rig — see OrderSelectionTaskDriver.PalletJack.cs, which turns it on/off).
///
/// Builds: an arms-only AvatarMask, the Carry_Case clip, a bool parameter <c>CarryCase</c>, and an
/// override layer "Carry" (Empty ⇄ CarryHold). The base layer keeps walking/idling underneath, so a
/// selector walks normally with their arms holding the case. Safe to re-run (rebuilds everything).
/// </summary>
public static class AddCarryLayerToStaffControllers
{
    const string MaskPath = "Assets/_Project/Animations/Anims/UpperBody_Arms.mask";
    const string ClipPath = "Assets/_Project/Animations/Anims/Carry_Case.anim";
    const string LayerName = "Carry";
    const string Param = "CarryCase";

    static readonly string[] Targets =
    {
        "Assets/_Project/Animations/AnimControllers/MaleStaff.controller",
        "Assets/_Project/Resources/Resource_AvatarSystemAssets/FemaleStaff.controller",
    };

    // Per arm, in HumanTrait order starting at "<Side> Shoulder Down-Up":
    // ShoulderDU, ShoulderFB, ArmDU, ArmFB, ArmTwist, ForearmStretch, ForearmTwist, HandDU, HandIO
    static readonly float[] ArmMuscles = { 0f, 0f, -0.4f, -0.1f, 0.3f, 0.4f, 0f, 0f, 0f };
    const int LeftArmFirstMuscle = 37, RightArmFirstMuscle = 46;

    [MenuItem("Tools/Add Carry Layer To Staff Controllers")]
    public static void Run()
    {
        var mask = BuildMask();
        var clip = BuildClip();

        foreach (var path in Targets)
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (c == null) { Debug.LogError("[AddCarryLayer] Missing " + path); continue; }

            bool has = false;
            foreach (var p in c.parameters) if (p.name == Param) has = true;
            if (!has) c.AddParameter(Param, AnimatorControllerParameterType.Bool);

            // Rebuild the layer from scratch so re-running never stacks duplicates.
            var layers = c.layers;
            for (int i = layers.Length - 1; i >= 0; i--)
                if (layers[i].name == LayerName) c.RemoveLayer(i);

            var sm = new AnimatorStateMachine { name = LayerName, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(sm, c);

            var empty = sm.AddState("Empty");
            empty.writeDefaultValues = false;
            var hold = sm.AddState("CarryHold");
            hold.motion = clip;
            hold.writeDefaultValues = false;
            sm.defaultState = empty;

            var on = empty.AddTransition(hold);
            on.hasExitTime = false; on.duration = 0.15f;
            on.AddCondition(AnimatorConditionMode.If, 0, Param);
            var off = hold.AddTransition(empty);
            off.hasExitTime = false; off.duration = 0.2f;
            off.AddCondition(AnimatorConditionMode.IfNot, 0, Param);

            c.AddLayer(new AnimatorControllerLayer
            {
                name = LayerName,
                stateMachine = sm,
                avatarMask = mask,
                // 0, not 1: on a humanoid rig the empty state still forces the arms into a reference pose at
                // weight 1. OrderSelectionTaskDriver.SetAnimBool raises the weight only while a case is held.
                defaultWeight = 0f,
                blendingMode = AnimatorLayerBlendingMode.Override,
            });

            EditorUtility.SetDirty(c);
            Debug.LogWarning("[AddCarryLayer] Updated " + path);
        }
        AssetDatabase.SaveAssets();
    }

    static AvatarMask BuildMask()
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, MaskPath);
        }
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
        EditorUtility.SetDirty(mask);
        return mask;
    }

    static AnimationClip BuildClip()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null)
        {
            clip = new AnimationClip { name = "Carry_Case" };
            AssetDatabase.CreateAsset(clip, ClipPath);
        }
        clip.ClearCurves();
        for (int side = 0; side < 2; side++)
        {
            int first = side == 0 ? LeftArmFirstMuscle : RightArmFirstMuscle;
            for (int k = 0; k < ArmMuscles.Length; k++)
            {
                string muscle = HumanTrait.MuscleName[first + k];
                var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), muscle);
                AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, 1f, ArmMuscles[k]));
            }
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }
}
