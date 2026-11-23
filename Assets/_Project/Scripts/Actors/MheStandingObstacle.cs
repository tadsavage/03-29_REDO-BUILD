using UnityEngine;
using UnityEngine.AI;

namespace GameCore.Actors
{
    /// <summary>
    /// A machine whose NavMeshAgent is OFF (parked, doing scripted pick / reverse-in / turn work, waiting) is invisible to every other
    /// agent's local avoidance, so a moving machine steers straight at it and the two lock up (three jacks in a triangle, 2026-11-22).
    /// This gives such a machine a NON-carving NavMeshObstacle (the NavMesh itself is untouched) that is on exactly while the agent is off,
    /// so the agents that ARE moving steer round it. Added to every machine in <c>AiNavigation.Awake</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public class MheStandingObstacle : MonoBehaviour
    {
        /// <summary>Avoidance footprint of a standing machine, metres (the agents' own radius is 1.0-1.25, which is too fat for a jack's body).</summary>
        public const float Radius = 0.9f;
        public const float Height = 2f;

        private NavMeshAgent _agent;
        private NavMeshObstacle _obstacle;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            // A prefab that already carries its own obstacle keeps its own settings: this helper then does nothing.
            if (GetComponent<NavMeshObstacle>() != null) { enabled = false; return; }

            _obstacle = gameObject.AddComponent<NavMeshObstacle>();
            _obstacle.carving = false;
            _obstacle.shape = NavMeshObstacleShape.Capsule;
            Vector3 s = transform.lossyScale;
            float sxz = Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z)));
            float sy = Mathf.Max(0.01f, Mathf.Abs(s.y));
            _obstacle.radius = Radius / sxz;
            _obstacle.height = Height / sy;
            _obstacle.center = new Vector3(0f, Height * 0.5f / sy, 0f);
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
            if (_obstacle.enabled != want) _obstacle.enabled = want;
        }
    }
}
