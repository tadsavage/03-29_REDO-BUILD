using UnityEngine;
using UnityEngine.AI;

namespace GameCore.Actors
{
    /// <summary>
    /// A machine whose NavMeshAgent is OFF (parked, doing scripted pick / reverse-in / turn work, waiting) is invisible to every other
    /// agent's local avoidance, so a moving machine steers straight at it and the two lock up (three jacks in a triangle, 2026-11-22).
    /// This gives such a machine a NON-carving NavMeshObstacle (the NavMesh itself is untouched) that is on exactly while the agent is off,
    /// so the agents that ARE moving steer round it. Added to every machine in <c>AiNavigation.Awake</c>.
    ///
    /// 2026-11-26 (Tad, screenshot): the obstacle was a 0.9 m capsule on the machine's pivot, so a pallet jack's REAR fork position (and the
    /// pallet on it) was invisible: other jacks rolled into it and reach trucks drove over the back half of jacks. It is now a BOX fitted to
    /// the machine's real outline - body, forks and whatever pallets ride on them - re-measured while it stands (a pallet can be added).
    /// Non-carving on purpose: carving would also cut the floor the selector stands on beside his jack at a pick face.
    /// </summary>
    [DisallowMultipleComponent]
    public class MheStandingObstacle : MonoBehaviour
    {
        /// <summary>Fallback footprint when the outline cannot be measured, metres.</summary>
        public const float Radius = 0.9f;
        public const float Height = 2f;
        /// <summary>Margin added round the measured outline, metres (each side).</summary>
        private const float Margin = 0.1f;
        private const float RefitInterval = 0.5f;

        private NavMeshAgent _agent;
        private NavMeshObstacle _obstacle;
        private float _nextFit;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            // A prefab that already carries its own obstacle keeps its own settings: this helper then does nothing.
            if (GetComponent<NavMeshObstacle>() != null) { enabled = false; return; }

            _obstacle = gameObject.AddComponent<NavMeshObstacle>();
            _obstacle.carving = false;
            _obstacle.shape = NavMeshObstacleShape.Box;
            FitFallback();
            _obstacle.enabled = false;
        }

        /// <summary>Call BEFORE switching a machine's agent on: an agent and an obstacle active together make Unity warn and misbehave
        /// (the per-frame <see cref="Update"/> alone leaves one frame of overlap).</summary>
        public static void Suspend(Component machine)
        {
            if (machine == null) return;
            var s = machine.GetComponent<MheStandingObstacle>();
            if (s != null && s._obstacle != null) s._obstacle.enabled = false;
        }

        private void Update()
        {
            if (_obstacle == null) return;
            bool want = _agent == null || !_agent.enabled;
            if (want && (!_obstacle.enabled || Time.unscaledTime >= _nextFit))
            {
                _nextFit = Time.unscaledTime + RefitInterval;
                FitToOutline();
            }
            if (_obstacle.enabled != want) _obstacle.enabled = want;
        }

        private void FitFallback()
        {
            Vector3 s = transform.lossyScale;
            float sxz = Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z)));
            float sy = Mathf.Max(0.01f, Mathf.Abs(s.y));
            _obstacle.size = new Vector3(2f * Radius / sxz, Height / sy, 2f * Radius / sxz);
            _obstacle.center = new Vector3(0f, Height * 0.5f / sy, 0f);
        }

        private static readonly Vector3[] s_corners = new Vector3[8];

        /// <summary>Box = the machine's renderers (incl. forks and carried pallets) in the machine's own local space. Skips floor shadow
        /// decals and anything under another agent (a rider standing on the machine).</summary>
        private void FitToOutline()
        {
            bool any = false;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var r in GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || !r.enabled) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (r.name.StartsWith("VehicleShadow")) continue;
                var otherAgent = r.GetComponentInParent<NavMeshAgent>();
                if (otherAgent != null && otherAgent != _agent) continue;

                Bounds lb;
                if (r is SkinnedMeshRenderer smr) lb = smr.localBounds;
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    lb = mf.sharedMesh.bounds;
                }
                Vector3 c = lb.center, e = lb.extents;
                int i = 0;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                            s_corners[i++] = transform.InverseTransformPoint(r.transform.TransformPoint(c + Vector3.Scale(e, new Vector3(x, y, z))));
                for (int k = 0; k < 8; k++) { min = Vector3.Min(min, s_corners[k]); max = Vector3.Max(max, s_corners[k]); }
                any = true;
            }
            if (!any) { FitFallback(); return; }

            Vector3 s = transform.lossyScale;
            float sx = Mathf.Max(0.01f, Mathf.Abs(s.x)), sz = Mathf.Max(0.01f, Mathf.Abs(s.z));
            Vector3 size = max - min;
            size.x += 2f * Margin / sx;
            size.z += 2f * Margin / sz;
            size.y = Mathf.Max(size.y, 0.1f);
            _obstacle.size = size;
            _obstacle.center = (min + max) * 0.5f;
        }
    }
}
