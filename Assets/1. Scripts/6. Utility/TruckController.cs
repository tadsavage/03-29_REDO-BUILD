using System.Collections;
using UnityEngine;

/// <summary>
/// Drives a truck through a fixed scripted route using direct Transform movement.
/// No NavMeshAgent — trucks follow predetermined waypoints, not pathfinding.
///
/// Entry:  Spawn → GateStop (guard check) → GateEnterNoTurn → Approach → PullPast
///         → Align → Bezier-arc reverse into dock → Docked (light red, timer)
/// Exit:   Docked → PullPast → GateLeaveNoTurn → ExitPoint → shrink + destroy
///         (light turns green on departure)
/// </summary>
public class TruckController : MonoBehaviour
{
    public enum TruckState
    {
        Idle,
        WaitingAtGate, GuardCheck,
        EnteringYard,
        Approaching, PullingPast,
        Aligning, Reversing,
        Docked,
        DepartingDock,
        LeavingYard,
        Exiting
    }

    [Header("Driving")]
    [SerializeField] private float driveSpeed       = 8f;
    [SerializeField] private float driveTurnSpeed   = 120f;
    [SerializeField] private float arrivedThreshold = 0.5f;

    [Header("Align (spin before reversing)")]
    [SerializeField] private float alignTurnSpeed      = 90f;
    [SerializeField] private float alignAngleTolerance = 1f;

    [Header("Reversing — Bezier arc")]
    [SerializeField] private float reverseSpeed = 3f;
    [Tooltip("0 = nearly straight, 1 = wide sweeping curve. 0.45 is a natural truck arc.")]
    [SerializeField] private float reverseArcTension = 0.45f;

    [Header("Guard check timing")]
    [SerializeField] private float guardApproachDuration = 2f;
    [SerializeField] private float guardCheckDuration    = 3f;
    [SerializeField] private float guardReturnDuration   = 2f;

    [Header("Unload & exit")]
    [SerializeField] private float unloadDuration = 7f;
    [SerializeField] private float exitShrinkTime = 1.2f;

    [Header("State (read-only in play)")]
    [SerializeField] private TruckState _state = TruckState.Idle;

    private DockSlot      _dock;
    private Vector3?      _gateStop;
    private Vector3?      _gateEnterNoTurn;
    private Vector3?      _gateLeaveNoTurn;
    private Vector3?      _exitWaypoint;
    private System.Action _onExited;
    private Quaternion    _targetAlignRot;
    private float         _stateTimer;
    private float         _groundY;
    private Vector3       _currentTarget;

    // Bezier reverse arc
    private Vector3 _bzP0, _bzP1, _bzP2, _bzP3;
    private float   _bzT;
    private float   _bzArcLen;

    public TruckState State => _state;

    private void Awake()
    {
        _groundY = transform.position.y;
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;
    }

    public void Init(Vector3? gateStop, Vector3? gateEnterNoTurn, Vector3? gateLeaveNoTurn,
                     Vector3? exitWaypoint, System.Action onExited)
    {
        _gateStop        = gateStop;
        _gateEnterNoTurn = gateEnterNoTurn;
        _gateLeaveNoTurn = gateLeaveNoTurn;
        _exitWaypoint    = exitWaypoint;
        _onExited        = onExited;
    }

    public void AssignAndGo(DockSlot dock)
    {
        if (dock == null || dock.IsOccupied)
        {
            Debug.LogWarning("[TruckController] Dock null or already occupied.");
            return;
        }
        _dock = dock;
        _dock.Claim();

        Vector3 firstTarget = _gateStop.HasValue ? _gateStop.Value : _dock.ApproachPoint;

        // Snap facing before first Update — no visible first-frame spin
        Vector3 dir = firstTarget - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);

        if (_gateStop.HasValue)
            SetTarget(TruckState.WaitingAtGate, _gateStop.Value);
        else
            SetTarget(TruckState.Approaching, firstTarget);
    }

    public void ForceDeparture()
    {
        if (_state == TruckState.Docked) BeginDeparture();
    }

    private void Update()
    {
        switch (_state)
        {
            case TruckState.WaitingAtGate:
                if (DriveToward(_currentTarget)) BeginGuardCheck();
                break;

            case TruckState.GuardCheck:
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f)
                {
                    if (_gateEnterNoTurn.HasValue)
                        SetTarget(TruckState.EnteringYard, _gateEnterNoTurn.Value);
                    else
                        SetTarget(TruckState.Approaching, _dock.ApproachPoint);
                }
                break;

            case TruckState.EnteringYard:
                if (DriveToward(_currentTarget))
                    SetTarget(TruckState.Approaching, _dock.ApproachPoint);
                break;

            case TruckState.Approaching:
                if (DriveToward(_currentTarget))
                    SetTarget(TruckState.PullingPast, _dock.PullPastPoint);
                break;

            case TruckState.PullingPast:
                if (DriveToward(_currentTarget)) BeginAlign();
                break;

            case TruckState.Aligning:
                StepAlign();
                break;

            case TruckState.Reversing:
                StepReverse();
                break;

            case TruckState.Docked:
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f) BeginDeparture();
                break;

            case TruckState.DepartingDock:
                if (DriveToward(_currentTarget))
                {
                    _dock.Release();
                    if (_gateLeaveNoTurn.HasValue)
                        SetTarget(TruckState.LeavingYard, _gateLeaveNoTurn.Value);
                    else
                        StartExiting();
                }
                break;

            case TruckState.LeavingYard:
                if (DriveToward(_currentTarget)) StartExiting();
                break;

            case TruckState.Exiting:
                if (DriveToward(_currentTarget))
                {
                    _state = TruckState.Idle;
                    StartCoroutine(ShrinkAndDestroy(exitShrinkTime));
                }
                break;
        }
    }

    // ── Direct forward movement ───────────────────────────────────────────────

    private bool DriveToward(Vector3 target)
    {
        Vector3 flat     = new Vector3(target.x, _groundY, target.z);
        Vector3 toTarget = flat - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude < arrivedThreshold)
        {
            transform.position = flat;
            return true;
        }

        Quaternion desired = Quaternion.LookRotation(toTarget.normalized);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, desired, driveTurnSpeed * Time.deltaTime);

        transform.position = Vector3.MoveTowards(
            transform.position, flat, driveSpeed * Time.deltaTime);

        return false;
    }

    // ── Bezier reverse arc ────────────────────────────────────────────────────

    private void BeginReverse()
    {
        var p0 = new Vector3(transform.position.x, _groundY, transform.position.z);
        var p3 = new Vector3(_dock.DockPosition.x,  _groundY, _dock.DockPosition.z);

        // fwd = the direction the truck's nose points (away from building / toward yard)
        var fwd = (_dock.DockRotation * Vector3.forward);
        fwd.y = 0f;
        fwd.Normalize();

        float chord   = Vector3.Distance(p0, p3);
        float tension = chord * reverseArcTension;

        // P1: pull the start tangent in the backing direction (nose faces yard, so back = -fwd)
        // P2: approach the dock from the yard side
        _bzP0 = p0;
        _bzP1 = p0 - fwd * tension;
        _bzP2 = p3 + fwd * tension;
        _bzP3 = p3;
        _bzT  = 0f;

        // Rough arc-length estimate for consistent speed (cubic Bezier ≈ chord * 1.5)
        _bzArcLen = chord * 1.5f;

        _state = TruckState.Reversing;
    }

    private void StepReverse()
    {
        _bzT += (reverseSpeed / Mathf.Max(_bzArcLen, 0.1f)) * Time.deltaTime;

        if (_bzT >= 1f)
        {
            transform.position = _bzP3;
            transform.rotation = _dock.DockRotation;
            _state = TruckState.Docked;
            OnDocked();
            return;
        }

        // Position follows the cubic Bezier exactly
        Vector3 pos = EvalBezier(_bzP0, _bzP1, _bzP2, _bzP3, _bzT);
        pos.y = _groundY;
        transform.position = pos;

        // Rotation follows -tangent (cab faces AWAY from direction of travel = reversing)
        Vector3 tangent = EvalBezierTangent(_bzP0, _bzP1, _bzP2, _bzP3, _bzT);
        tangent.y = 0f;
        if (tangent.sqrMagnitude > 0.001f)
        {
            Quaternion desired = Quaternion.LookRotation(-tangent.normalized);
            // 300 deg/sec keeps rotation glued to the arc without any lag or jerk
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, desired, 300f * Time.deltaTime);
        }
    }

    private static Vector3 EvalBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return u*u*u*p0 + 3f*u*u*t*p1 + 3f*u*t*t*p2 + t*t*t*p3;
    }

    private static Vector3 EvalBezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return 3f*u*u*(p1-p0) + 6f*u*t*(p2-p1) + 3f*t*t*(p3-p2);
    }

    // ── Phase transitions ─────────────────────────────────────────────────────

    private void SetTarget(TruckState next, Vector3 destination)
    {
        _state         = next;
        _currentTarget = destination;
    }

    private void BeginGuardCheck()
    {
        _state      = TruckState.GuardCheck;
        _stateTimer = guardApproachDuration + guardCheckDuration + guardReturnDuration;
    }

    private void BeginAlign()
    {
        _targetAlignRot = _dock.DockRotation;
        _state          = TruckState.Aligning;
    }

    private void StepAlign()
    {
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, _targetAlignRot, alignTurnSpeed * Time.deltaTime);

        if (Quaternion.Angle(transform.rotation, _targetAlignRot) < alignAngleTolerance)
        {
            transform.rotation = _targetAlignRot;
            BeginReverse();
        }
    }

    private void OnDocked()
    {
        _dock.LightController?.SetOccupied(true);
        _stateTimer = unloadDuration;
        Debug.Log($"[TruckController] {name} docked at door {_dock.DoorNumber}. Unloading ({unloadDuration}s).");
    }

    private void BeginDeparture()
    {
        _dock.LightController?.SetOccupied(false);
        SetTarget(TruckState.DepartingDock, _dock.PullPastPoint);
    }

    private void StartExiting()
    {
        if (_exitWaypoint.HasValue)
            SetTarget(TruckState.Exiting, _exitWaypoint.Value);
        else
        {
            _state = TruckState.Idle;
            StartCoroutine(ShrinkAndDestroy(exitShrinkTime));
        }
    }

    private IEnumerator ShrinkAndDestroy(float duration)
    {
        Vector3 startScale = transform.localScale;
        float   elapsed    = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, elapsed / duration);
            yield return null;
        }
        _onExited?.Invoke();
        Destroy(gameObject);
    }
}
