using UnityEngine;
using UnityEngine.AI;

namespace GameCore.Actors
{
    /// <summary>
    /// NO MACHINE ROLLS INTO ANOTHER WHILE ITS NAV AGENT IS OFF (Tad, 2026-11-22: two jacks in an aisle, the rear one rolled up onto the
    /// one in front and clipped through it by a metre).
    ///
    /// With the agent off a machine is moved by script (turns, straight slides, hand-driven lane / rack moves), and none of those look at
    /// other machines. This catches every one of them in one place: after all scripts have run for the frame, if the machine moved and the
    /// new pose brings its outline closer than <see cref="Clearance"/> to another machine without moving away from it, the move is undone
    /// (the mover simply makes no progress until the way is clear). Moving AWAY from a machine you are already too close to is always allowed,
    /// so a machine restored overlapping something can still back out. While the agent is ON the guard does nothing: the agents steer round each
    /// other themselves and "do just fine".
    /// Added to every machine in <c>AiNavigation.Awake</c>.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public class MheClipGuard : MonoBehaviour
    {
        /// <summary>Closest an outline may get to another machine's outline (m).</summary>
        public const float Clearance = 0.25f;
        /// <summary>A jump bigger than this in one frame is a teleport (load, warp, respawn), not a drive: accepted as it is.</summary>
        private const float MaxStep = 2.5f;

        private MHEOperatorSlot _slot;
        private NavMeshAgent _agent;
        private Rigidbody _rb;
        private Vector3 _lastPos;
        private Quaternion _lastRot;
        private bool _have;
        private float _warnedAt = -100f;

        private void Awake()
        {
            _slot = GetComponent<MHEOperatorSlot>();
            _agent = GetComponent<NavMeshAgent>();
            _rb = GetComponent<Rigidbody>();
        }

        private void OnEnable() { _have = false; }

        private void LateUpdate()
        {
            // Pallet jacks only (the case Tad reported). Reach trucks and dock stockers have long hand-driven lane / trailer routines with no
            // "blocked" handling of their own: a guard there could stall them, so they are left as they were until they get the same care.
            if (_slot == null || !_slot.IsPalletJack) return;

            Vector3 pos = transform.position;
            Quaternion rot = transform.rotation;
            if (!_have) { _lastPos = pos; _lastRot = rot; _have = true; return; }

            float stepSq = (pos - _lastPos).sqrMagnitude;
            if (stepSq < 1e-6f && Quaternion.Angle(rot, _lastRot) < 0.05f) return;

            bool agentOn = _agent != null && _agent.enabled;
            if (agentOn || _slot == null || stepSq > MaxStep * MaxStep)
            {
                _lastPos = pos; _lastRot = rot;
                return;
            }

            // Judge the move from where the machine WAS: put it back, ask whether stepping to the new pose is allowed, then keep whichever.
            transform.SetPositionAndRotation(_lastPos, _lastRot);
            bool blocked = MachineClearance.VehicleSpacingBlocked(_slot, pos, rot, Clearance, out var blocker);
            if (!blocked)
            {
                transform.SetPositionAndRotation(pos, rot);
                _lastPos = pos; _lastRot = rot;
                return;
            }

            if (_rb != null) { _rb.position = _lastPos; _rb.rotation = _lastRot; }
            if (Time.time - _warnedAt > 20f)
            {
                _warnedAt = Time.time;
                Debug.Log($"[MheClipGuard] '{name}' held back: moving would bring it within {Clearance:0.00} m of '{(blocker != null ? blocker.name : "another machine")}'.");
            }
        }
    }
}
