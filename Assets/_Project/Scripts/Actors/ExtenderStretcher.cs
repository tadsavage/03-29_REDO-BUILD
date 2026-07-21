using UnityEngine;

namespace GameCore.Actors
{
    /// <summary>
    /// Visually "grows" this transform along local Z to bridge the gap between the mast and the
    /// forks as they extend, creating the illusion the forks are still attached via a telescoping
    /// arm. Never shrinks below its authored rest length (the resting look already reaches as far
    /// as it's modeled to); only stretches further once the target moves beyond that.
    ///
    /// Scaling happens around this transform's own pivot, which sits outside the mesh entirely
    /// (confirmed via mesh bounds), so a bare scale change would slide the whole piece rather than
    /// stretch it from a fixed point. Each frame this also re-derives local position so the near
    /// (mast-attached) edge stays pinned at its original spot while only the far edge moves.
    ///
    /// Assumes the target shares a parent whose own Z offset is always zero (true for Mast, which
    /// only moves on Y -- see MastYFollower) so the target's local Z is directly comparable to this
    /// object's own local Z without extra conversion.
    /// </summary>
    public class ExtenderStretcher : MonoBehaviour
    {
        [Tooltip("Transform to track (the forks). Its local Z, relative to this object's parent, is the far reach point.")]
        [SerializeField] private Transform _target;

        [Tooltip("How much faster than the forks' actual movement this stretches once past its natural rest length (2 = grows twice as fast/far per unit the forks move beyond rest).")]
        [SerializeField] private float _extensionRateMultiplier = 2f;

        private float _restNearZ;   // mesh's mast-side edge, in this object's REST local space
        private float _restFarZ;    // mesh's far (fork-side) edge, in this object's REST local space
        private float _restLength;
        private Vector3 _restLocalPosition;
        private Vector3 _restLocalScale;

        private float _forkRestZ;
        private bool  _forkRestInitialized;

        private void Awake()
        {
            _restLocalPosition = transform.localPosition;
            _restLocalScale = transform.localScale;

            var mf = GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            Bounds meshBounds = mf.sharedMesh.bounds;
            // The far (fork-side) edge extends in the SAME direction the forks extend -- negative
            // local Z for this rig -- so it's the more-negative of the two edges.
            _restNearZ = meshBounds.center.z + meshBounds.extents.z;
            _restFarZ  = meshBounds.center.z - meshBounds.extents.z;
            _restLength = _restNearZ - _restFarZ;
        }

        private void LateUpdate()
        {
            if (_target == null || _restLength <= 0.0001f) return;

            float targetZ = _target.localPosition.z;

            // Establish (and self-correct) the forks' own rest Z the first time we see it, so
            // growth starts the INSTANT the forks move away from rest -- not once they happen to
            // cross this mesh's own natural extent, which left a dead-zone/delay at the start of
            // every extension (forks visibly sliding out while this stayed static).
            if (!_forkRestInitialized || targetZ > _forkRestZ)
            {
                _forkRestZ = targetZ;
                _forkRestInitialized = true;
            }

            float forkDelta = _forkRestZ - targetZ; // >= 0, how far past its own rest the forks have moved
            float desiredFarZ = _restFarZ - forkDelta * _extensionRateMultiplier;
            float scale = (_restNearZ - desiredFarZ) / _restLength;

            var s = _restLocalScale;
            s.z *= scale;
            transform.localScale = s;

            var p = _restLocalPosition;
            p.z = _restLocalPosition.z + _restNearZ * _restLocalScale.z * (1f - scale);
            transform.localPosition = p;
        }
    }
}
