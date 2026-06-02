using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Drives a truck through the full dock sequence:
///   Approach → PullPast → Align (turn around) → Reverse into dock → Docked
///
/// NavMeshAgent handles forward driving. The agent is disabled for the
/// Align + Reverse phases — position and rotation are controlled directly,
/// which gives the natural arc a real backing maneuver produces.
///
/// Setup:
///   1. Add this component to the truck prefab (alongside its NavMeshAgent).
///   2. Call AssignAndGo(DockSlot) from TruckSpawner after instantiation.
///   3. Place DockSlot child transforms in the scene to define the path.
/// </summary>
public class TruckController : MonoBehaviour
{
    public enum TruckState { Idle, Approaching, PullingPast, Aligning, Reversing, Docked }

    [Header("Forward driving")]
    [SerializeField] private float arrivedThreshold = 0.6f;

    [Header("Align (spin-in-place before reversing)")]
    [SerializeField] private float alignTurnSpeed = 70f;   // deg/sec
    [SerializeField] private float alignAngleTolerance = 2f;

    [Header("Reversing")]
    [SerializeField] private float reverseSpeed = 3.5f;
    [SerializeField] private float reverseSteerSpeed = 45f; // deg/sec — how quickly it corrects
    [SerializeField] private float dockStopDistance = 0.3f;

    [Header("State (read-only in play)")]
    [SerializeField] private TruckState _state = TruckState.Idle;

    private NavMeshAgent _agent;
    private DockSlot     _dock;
    private Quaternion   _targetAlignRot;

    public TruckState State => _state;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (_agent != null) _agent.enabled = false;
    }

    /// <summary>Called by TruckSpawner immediately after instantiation.</summary>
    public void AssignAndGo(DockSlot dock)
    {
        if (dock == null || dock.IsOccupied)
        {
            Debug.LogWarning("[TruckController] Dock null or already occupied.");
            return;
        }

        _dock = dock;
        _dock.Claim();

        _agent.enabled = true;
        GoTo(TruckState.Approaching, _dock.ApproachWaypoint.position);
    }

    private void Update()
    {
        switch (_state)
        {
            case TruckState.Approaching:
                if (HasArrived())
                    GoTo(TruckState.PullingPast, _dock.PullPastPoint.position);
                break;

            case TruckState.PullingPast:
                if (HasArrived())
                    BeginAlign();
                break;

            case TruckState.Aligning:
                StepAlign();
                break;

            case TruckState.Reversing:
                StepReverse();
                break;
        }
    }

    // ── Phase transitions ─────────────────────────────────────────────────────

    private void GoTo(TruckState next, Vector3 destination)
    {
        _state = next;
        _agent.enabled = true;
        _agent.SetDestination(destination);
    }

    private void BeginAlign()
    {
        _agent.enabled = false;

        // The truck must face DockTarget.forward (nose toward yard) so that
        // moving backward (-transform.forward) carries it into the dock.
        _targetAlignRot = _dock.DockTarget.rotation;
        _state = TruckState.Aligning;
    }

    private void StepAlign()
    {
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, _targetAlignRot, alignTurnSpeed * Time.deltaTime);

        if (Quaternion.Angle(transform.rotation, _targetAlignRot) < alignAngleTolerance)
        {
            transform.rotation = _targetAlignRot;
            _state = TruckState.Reversing;
        }
    }

    private void StepReverse()
    {
        Vector3 toTarget = _dock.DockTarget.position - transform.position;
        float   dist     = toTarget.magnitude;

        if (dist > dockStopDistance)
        {
            // Steer: rotate so our -forward gradually points at the dock target.
            // LookRotation(-toTarget) = nose pointing AWAY from dock = back toward dock.
            if (toTarget.sqrMagnitude > 0.001f)
            {
                Quaternion desired = Quaternion.LookRotation(-toTarget.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, desired, reverseSteerSpeed * Time.deltaTime);
            }

            // Drive backward along current -forward
            transform.position -= transform.forward * reverseSpeed * Time.deltaTime;
        }
        else
        {
            // Snap to exact docked pose
            transform.position = _dock.DockTarget.position;
            transform.rotation = _dock.DockTarget.rotation;
            _state = TruckState.Docked;
            OnDocked();
        }
    }

    private void OnDocked()
    {
        Debug.Log($"[TruckController] {gameObject.name} docked at {_dock.name}.");
        // Future: open ShippingDoor, spawn driver, trigger offload sequence
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool HasArrived()
    {
        if (_agent == null || !_agent.enabled || _agent.pathPending) return false;
        return _agent.remainingDistance <= _agent.stoppingDistance + arrivedThreshold;
    }
}
