using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages equipping and unequipping RF gun and clipboard props for employees assigned
/// to receiving work. Props are anchored to left/right hand bones dynamically based on
/// the character's animator hierarchy, supporting modular avatar systems.
/// </summary>
public static class ReceivingEquipmentService
{
    // Final local Transform values relative to the hand bone, tuned by hand in Play Mode (Tad —
    // 2026-07-05) by nudging the live-instantiated prop until it sat correctly in the palm, then
    // reading the exact numbers back off the Inspector. These are absolute values, not offsets —
    // set directly onto the prop's transform below, not added on top of the prefab's own pivot.
    private static readonly Vector3 ClipboardLocalPosition = new Vector3(0.035f, 0.248f, 0.087f);
    private static readonly Vector3 ClipboardLocalEuler = new Vector3(11.683f, -6.183f, 89.448f);
    private static readonly Vector3 ClipboardLocalScale = new Vector3(1f, 1f, 1f);

    private static readonly Vector3 RfGunLocalPosition = new Vector3(0f, 0.175f, 0.08f);
    private static readonly Vector3 RfGunLocalEuler = new Vector3(-90f, 0f, -180f);
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

        #if UNITY_EDITOR
        clipboardPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Workers/_Clipboard.prefab");
        rfGunPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Workers/_Scan_Gun.prefab");
        #endif

        // Fallback to Resources folder at runtime
        if (clipboardPrefab == null)
            clipboardPrefab = Resources.Load<GameObject>("Workers/_Clipboard");
        if (rfGunPrefab == null)
            rfGunPrefab = Resources.Load<GameObject>("Workers/_Scan_Gun");

        if (clipboardPrefab == null || rfGunPrefab == null)
        {
            Debug.LogError("[ReceivingEquipmentService] Could not load prefabs. Checked: Assets/_Project/Prefabs/Workers/ and Resources/Workers/");
            return;
        }

        var clipboardInstance = Object.Instantiate(clipboardPrefab, rightHand);
        var rfGunInstance = Object.Instantiate(rfGunPrefab, leftHand);

        clipboardInstance.name = "_Clipboard";
        rfGunInstance.name = "_Scan_Gun";

        // Instantiate(prefab, parent) keeps the prefab's own authored local Transform, which doesn't
        // naturally align with a held pose relative to the hand bone's local axes — set directly to
        // the tuned values instead.
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
