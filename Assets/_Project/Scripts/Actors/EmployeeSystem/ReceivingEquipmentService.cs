using UnityEngine;
using Unity.AI.Navigation;
using System.Collections.Generic;

/// <summary>
/// Manages equipping and unequipping RF gun and clipboard props for employees assigned
/// to receiving work. Props are anchored to left/right hand bones dynamically based on
/// the character's animator hierarchy, supporting modular avatar systems.
/// </summary>
public static class ReceivingEquipmentService
{
    // Final local Transform values relative to the hand bone, tuned by hand in Play Mode (Tad —
    // 2026-07-05, re-tuned 2026-09-26 for the left-hand anchor) by nudging the live-instantiated
    // prop until it sat correctly in the palm, then reading the exact numbers back off the
    // Inspector. These are absolute values, not offsets — set directly onto the prop's transform
    // below, not added on top of the prefab's own pivot. Anchored to Wrist_L (the bone Unity's
    // Humanoid rig resolves HumanBodyBones.LeftHand to on this rig — see FindHandBone).
    //
    // A short-lived male/female split existed here (2026-09-26/27) to compensate for a mirrored
    // bind pose on an experimental male modular body rig (male_body_floor.fbx) that has since been
    // removed — both genders now use the same Polyperfect man_construction_worker/
    // woman_construction_worker fixed-avatar pair (EmployeeSpawner.FixedAvatarFor), which share an
    // identical bind pose. Re-verified live 2026-09-27: Animator.Rebind() on both a male and female
    // employee's FixedAvatar reads the exact same LeftHand/RightHand local rotation
    // (355.34, 348.60, 2.55) — no mirroring, single shared constant is correct again.
    private static readonly Vector3 ClipboardLocalPosition = new Vector3(0.1224816f, 0.00781303f, 0.05416707f);
    private static readonly Vector3 ClipboardLocalEuler = new Vector3(85f, -64f, -53.309f);
    private static readonly Vector3 ClipboardLocalScale = new Vector3(1f, 1f, 1f);

    private static readonly Vector3 RfGunLocalPosition = new Vector3(-0.152f, 0.071f, -0.062f);
    private static readonly Vector3 RfGunLocalEuler = new Vector3(-158.66f, -107.957f, -166.741f);
    private static readonly Vector3 RfGunLocalScale = new Vector3(1f, 1f, 1f);

    private static Dictionary<EmployeeIdentity, ReceivingEquipment> _equippedEmployees = new();

    private class ReceivingEquipment
    {
        public GameObject ClipboardInstance;
        public GameObject RFGunInstance;
        public Transform LeftHandBone;
        public Transform RightHandBone;
    }

    /// <summary>Equips an employee with RF gun (left hand) and clipboard (right hand).</summary>
    public static void Equip(EmployeeIdentity identity)
    {
        if (identity == null) return;

        // Already equipped
        if (_equippedEmployees.ContainsKey(identity))
            return;

        var animator = FindAvatarAnimator(identity);
        if (animator == null)
        {
            Debug.LogWarning($"[ReceivingEquipmentService] {identity.name} has no Animator component");
            return;
        }

        // Find hand bones
        var leftHand = FindHandBone(animator, "left");
        var rightHand = FindHandBone(animator, "right");

        if (leftHand == null || rightHand == null)
        {
            Debug.LogWarning($"[ReceivingEquipmentService] Could not find hand bones on {identity.name}");
            return;
        }

        // Load and instantiate props (try AssetDatabase first in editor, then Resources)
        GameObject clipboardPrefab = null;
        GameObject rfGunPrefab = null;

        // 2026-09-27: Assets/_Project/Prefabs/WORKERS/WORKER_ACCESSORIES/ (the old hand-made prefab
        // location) no longer exists — the Blender source folder tree was reorganized to
        // Modular_Staff_Models. _ClipBoard/_Scangun are leading-underscore "held props" (Tad's own
        // convention, see ModularAvatarImporter.FixNewExport), deliberately excluded from the AOD
        // finalization pipeline (they're not gender_slot_variant parts), so they were never migrated
        // into ModularAvatarFinalizer's output folder either — load the raw FBX directly instead,
        // same as any other model asset. No hand-made prefab wrapper is needed for these.
        #if UNITY_EDITOR
        clipboardPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Models/BlenderFiles/Modular_Staff_Models/PROPS_MODELS/_ClipBoard.fbx");
        rfGunPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Models/BlenderFiles/Modular_Staff_Models/PROPS_MODELS/_Scangun.fbx");
        #endif

        // Fallback to Resources folder at runtime (a BUILT player strips the AssetDatabase block
        // above entirely) — no Resources/Workers folder currently exists, so this always misses in
        // a real build. Known gap, same class of issue as the other Assets/_Saves-in-project-folder
        // shipping blockers already tracked in CLAUDE.md; harmless in-Editor since the block above
        // resolves first there.
        if (clipboardPrefab == null)
            clipboardPrefab = Resources.Load<GameObject>("Resource_AvatarSystemAssets/_ClipBoard");
        if (rfGunPrefab == null)
            rfGunPrefab = Resources.Load<GameObject>("Resource_AvatarSystemAssets/_Scangun");

        if (clipboardPrefab == null || rfGunPrefab == null)
        {
            Debug.LogError("[ReceivingEquipmentService] Could not load prefabs. Checked: Assets/_Project/Models/BlenderFiles/Modular_Staff_Models/PROPS_MODELS/ and Resources/Resource_AvatarSystemAssets/");
            return;
        }

        // Scan gun in the RIGHT hand, clipboard in the LEFT hand (Tad, 2026-09-26) — was reversed.
        var clipboardInstance = Object.Instantiate(clipboardPrefab, leftHand);
        var rfGunInstance = Object.Instantiate(rfGunPrefab, rightHand);

        // Ensure props are excluded from NavMesh baking. Without this, Unity tries to include
        // their meshes in the bake, which fails because these meshes are not marked as readable.
        var clipMod = clipboardInstance.AddComponent<NavMeshModifier>();
        clipMod.ignoreFromBuild = true;
        var gunMod = rfGunInstance.AddComponent<NavMeshModifier>();
        gunMod.ignoreFromBuild = true;

        clipboardInstance.name = "_Clipboard";
        rfGunInstance.name = "_Scan_Gun";

        // The infra-red scan beam (a modeled "Scan_Ray" mesh child on the gun prefab) must start
        // OFF — it's only switched on for the duration of the receiving fill bar (see
        // ReceiverReceivingWorkflow.SetInfraRedBeam). The prefab authors it active by default (so
        // it's visible while editing/placing the child in the Editor), so force it off the instant
        // it's equipped rather than leaving it on until this employee's first completed receive.
        var scanRay = rfGunInstance.transform.Find("Scan_Ray");
        if (scanRay != null) scanRay.gameObject.SetActive(false);

        // Instantiate(prefab, parent) keeps the prefab's own authored local Transform, which doesn't
        // naturally align with a held pose relative to the hand bone's local axes — set directly to
        // the tuned values instead. Same values for both genders — see the field comments above.
        clipboardInstance.transform.localPosition = ClipboardLocalPosition;
        clipboardInstance.transform.localRotation = Quaternion.Euler(ClipboardLocalEuler);
        clipboardInstance.transform.localScale = ClipboardLocalScale;

        rfGunInstance.transform.localPosition = RfGunLocalPosition;
        rfGunInstance.transform.localRotation = Quaternion.Euler(RfGunLocalEuler);
        rfGunInstance.transform.localScale = RfGunLocalScale;

        var equipment = new ReceivingEquipment
        {
            ClipboardInstance = clipboardInstance,
            RFGunInstance = rfGunInstance,
            LeftHandBone = leftHand,
            RightHandBone = rightHand
        };

        _equippedEmployees[identity] = equipment;
        Debug.Log($"[ReceivingEquipmentService] Equipped {identity.name} with RF gun and clipboard");
    }

    /// <summary>Returns the currently-equipped RF gun instance for an employee, or null if not
    /// equipped. Callers that need to look up something specific to the RF gun prop itself (e.g. the
    /// Infra-Red LineRenderer child) must search under this instance, NOT the whole employee — the
    /// employee root can carry its own unrelated LineRenderer (NavAgentGuidance's path-guidance line),
    /// which GetComponentInChildren would otherwise match first.</summary>
    public static GameObject GetRfGunInstance(EmployeeIdentity identity)
    {
        if (identity != null && _equippedEmployees.TryGetValue(identity, out var equipment))
            return equipment.RFGunInstance;
        return null;
    }

    /// <summary>Unequips an employee, removing the RF gun and clipboard props.</summary>
    public static void Unequip(EmployeeIdentity identity)
    {
        if (identity == null || !_equippedEmployees.TryGetValue(identity, out var equipment))
            return;

        if (equipment.ClipboardInstance != null)
            Object.Destroy(equipment.ClipboardInstance);

        if (equipment.RFGunInstance != null)
            Object.Destroy(equipment.RFGunInstance);

        _equippedEmployees.Remove(identity);
        Debug.Log($"[ReceivingEquipmentService] Unequipped {identity.name}");
    }

    /// <summary>
    /// Finds the Animator that actually has a real skeleton to anchor props to. Employees have TWO
    /// Animators: a logic-only "worker" one on the root (driven by AgentAnimation — no real bones,
    /// see AiNavigation/ModularAvatarRig comments) and the visible modular avatar's own Animator,
    /// nested as a child alongside a ModularAvatarRig component (EmployeeSpawner wires them up as
    /// avatar.AddComponent&lt;ModularAvatarRig&gt;().Init(workerAnimator, modAnimator, ...)). The
    /// worker Animator can even report isHuman=true (its Avatar asset is a valid Humanoid asset,
    /// sometimes literally copy-pasted onto the placeholder), but GetBoneTransform on it returns
    /// null because there's no matching bone hierarchy underneath — only the avatar's own Animator
    /// has real "Hand.L"/"Hand.R" transforms. Falls back to the root's own Animator for any
    /// non-modular/legacy employee that has no ModularAvatarRig at all.
    /// </summary>
    private static Animator FindAvatarAnimator(EmployeeIdentity identity)
    {
        var rig = identity.GetComponentInChildren<ModularAvatarRig>(true);
        if (rig != null)
        {
            var avatarAnimator = rig.GetComponent<Animator>();
            if (avatarAnimator != null) return avatarAnimator;
        }

        return identity.GetComponent<Animator>();
    }

    /// <summary>Finds left or right hand bone in animator hierarchy, supporting modular avatars.</summary>
    private static Transform FindHandBone(Animator animator, string side)
    {
        if (animator == null) return null;

        // Most reliable path for a rigged Humanoid avatar (which the modular worker rigs are) —
        // Unity already knows exactly which bone is the hand regardless of naming convention.
        if (animator.isHuman)
        {
            var boneId = side == "left" ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
            var humanBone = animator.GetBoneTransform(boneId);
            if (humanBone != null) return humanBone;
        }

        // Fallback for non-Humanoid (Generic) rigs: name-based search. GetComponentsInChildren is
        // recursive (unlike Transform.Find, which only checks direct children — the previous version
        // of this fallback used Find() with names like "Hand.L", which silently never matched because
        // the hand bone sits several levels deep under Shoulder.L/UpperArm.L/LowerArm.L).
        var bones = animator.transform.GetComponentsInChildren<Transform>();
        Transform best = null;
        foreach (var bone in bones)
        {
            string lower = bone.name.ToLowerInvariant();
            if (!lower.Contains("hand")) continue;
            if (lower.Contains("end")) continue; // skip IK tip bones like "Hand.L_end"
            if (!MatchesSide(lower, side)) continue;

            // Prefer the shortest matching name — the base hand bone, not a longer nested variant.
            if (best == null || bone.name.Length < best.name.Length)
                best = bone;
        }

        return best;
    }

    /// <summary>Matches full words ("lefthand") AND the common single-letter suffix convention
    /// ("Hand.L", "Hand_L", "Hand L") used by the worker rig bones shown in the hierarchy.</summary>
    private static bool MatchesSide(string lowerName, string side)
    {
        if (lowerName.Contains(side)) return true;

        char letter = side[0]; // 'l' or 'r'
        foreach (var sep in new[] { '.', '_', ' ' })
        {
            if (lowerName.EndsWith(sep + letter.ToString())) return true;
        }
        return false;
    }

    /// <summary>Clean up on application quit or scene load.</summary>
    public static void ClearAll()
    {
        foreach (var equipment in _equippedEmployees.Values)
        {
            if (equipment.ClipboardInstance != null)
                Object.Destroy(equipment.ClipboardInstance);
            if (equipment.RFGunInstance != null)
                Object.Destroy(equipment.RFGunInstance);
        }
        _equippedEmployees.Clear();
    }
}
