using UnityEngine;

namespace GameCore.Actors
{
    /// <summary>
    /// Makes this transform's local Y position continuously mirror a target's local Y (e.g. the
    /// reach truck's mast rising/lowering in tandem with the forks carriage), while leaving its
    /// own X/Z untouched -- so it never reacts to the target extending/retracting on Z.
    /// Requires the target to share this object's parent (same local-space frame) so "local Y"
    /// means the same physical height for both.
    /// Runs in LateUpdate so it always reflects the target's position AFTER whatever moved it
    /// that same frame, avoiding a one-frame lag.
    /// </summary>
    public class MastYFollower : MonoBehaviour
    {
        [Tooltip("Transform whose local Y position this object should continuously mirror. Must share this object's parent.")]
        [SerializeField] private Transform _target;

        private void LateUpdate()
        {
            if (_target == null) return;

            Vector3 p = transform.localPosition;
            p.y = _target.localPosition.y;
            transform.localPosition = p;
        }
    }
}
