using UnityEngine;

/// <summary>
/// Simple procedural forward lean (bend at the spine and chest) layered on top of whatever the Animator is
/// playing. Used by order selectors reaching for a case in the far half of a pick slot (Tad, 2026-10-16) —
/// no reach animation exists, and this is "just something simple".
///
/// Runs after the Animator (LateUpdate, late execution order) on every humanoid Animator in the body (the
/// base rig and the visible modular avatar), so it never accumulates: the Animator rewrites the bones each
/// frame and this bends them again.
/// </summary>
[DefaultExecutionOrder(1000)]
public class BodyLean : MonoBehaviour
{
    [Tooltip("Total forward bend at full lean, degrees (60% spine, 40% chest).")]
    [SerializeField] private float _maxAngle = 35f;
    [Tooltip("How fast the lean blends in/out (weight per second).")]
    [SerializeField] private float _blendSpeed = 4f;

    private float _weight;
    private float _target;
    private Animator[] _animators;

    /// <summary>0 = upright, 1 = full lean.</summary>
    public void SetLean(float target) => _target = Mathf.Clamp01(target);

    private void LateUpdate()
    {
        _weight = Mathf.MoveTowards(_weight, _target, _blendSpeed * Time.deltaTime);
        if (_weight <= 0.001f) return;

        if (_animators == null || _animators.Length == 0 || System.Array.Exists(_animators, a => a == null))
            _animators = GetComponentsInChildren<Animator>(true);

        Vector3 axis = transform.right;
        float a = _maxAngle * _weight;
        foreach (var anim in _animators)
        {
            if (anim == null || !anim.isActiveAndEnabled || !anim.isHuman) continue;
            Transform spine = anim.GetBoneTransform(HumanBodyBones.Spine);
            Transform chest = anim.GetBoneTransform(HumanBodyBones.Chest);
            if (spine != null) spine.rotation = Quaternion.AngleAxis(a * 0.6f, axis) * spine.rotation;
            if (chest != null) chest.rotation = Quaternion.AngleAxis(a * 0.4f, axis) * chest.rotation;
        }
    }
}
