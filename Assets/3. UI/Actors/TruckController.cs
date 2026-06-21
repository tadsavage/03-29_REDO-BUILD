using System.Collections;
using UnityEngine;

/// <summary>
/// Drives a truck through a simple scripted yard route using direct Transform movement.
/// No NavMeshAgent — trucks follow a fixed set of waypoints.
///
/// Route (clean 4-point maneuver + guard gate):
///   Spawn → GateStop (guard inspection)
///   → Xform 1  GateEnterNoTurn        (drive through, NO stop)
///   → Xform 2  _drApproach-DepartPoint (drive straight, NO stop)
///   → Xform 3  _drBackup               (slight curve in, then STOP, wait 1.5s, NO mesh spin)
///   → reverse Bézier into the assigned door  (≈30° tractor/trailer jackknife)
///   → Docked (unload timer)
///   → pull out forward to Xform 2
///   → Xform 5  GateLeaveNoTurn         (drive through, NO stop)
///   → Exit point → shrink + destroy
///
/// Xform 2 and Xform 3 are SINGLE SHARED waypoints (named children of the guard
/// shack) used by every truck regardless of which door it's assigned. The door
/// (Xform 4) is the assigned DockSlot's DockPosition / DockRotation — the reverse
/// into the door is unchanged from before.
/// </summary>
public class TruckController : MonoBehaviour
{
    public enum TruckState
    {
        Idle,
        Queuing, GuardCheck,   // lining up at / holding the gate
        ToEnterNoTurn,     // → Xform 1
        ToApproach,        // → Xform 2
        ToBackup,          // → Xform 3 (slight curve)
        WaitingAtBackup,   // stop at Xform 3, 1.5s
        Reversing,         // Xform 3 → beginBackupTurn (curve)
        ReversingToDock,   // beginBackupTurn → dock (straight back-in)
        Docked,
        DepartToApproach,  // door → Xform 2
        ToLeaveNoTurn,     // Xform 2 → Xform 5
        ToExit,            // Xform 5 → Exit point
        Exiting
    }

    [Header("Driving")]
    [SerializeField] private float driveSpeed       = 2.5f;
    [SerializeField] private float driveTurnSpeed   = 180f;
    [SerializeField] private float arrivedThreshold = 0.5f;

    [Header("Forward Bézier curve (Xform 2 → 3 and depart)")]
    [Tooltip("0 = nearly straight, 1 = wide sweeping curve. 0.45 reads as a natural truck arc.")]
    [SerializeField] private float forwardDriveTension = 0.45f;

    [Header("Backup wait")]
    [Tooltip("Seconds the truck sits still at Xform 3 before it starts backing up.")]
    [SerializeField] private float backupWaitDuration = 1.5f;

    [Header("Reversing — Bézier curve into the door")]
    [SerializeField] private float reverseSpeed = 3f;
    [Tooltip("0 = nearly straight, 1 = wide sweeping curve. 0.45 is a natural truck arc. Raise it to widen the back-in turn toward the blue-line shape.")]
    [SerializeField] private float reverseArcTension = 0.45f;
    [Tooltip("Begin the final back-in curve this far BEFORE the truck root reaches beginBackupTurn (≈ half the truck length). Blends the straight reverse into the curve as one smooth S instead of a kink.")]
    [SerializeField] private float turnLeadDistance = 7f;

    [Header("Cab steering (tractor yaws at the hitch)")]
    [Tooltip("Turn the cab on its Y axis so it leads into curves, like a real tractor pivoting at the fifth wheel. Pure yaw — never touches X/Z.")]
    [SerializeField] private bool  articulateCab   = true;
    [Tooltip("Name of the cab child transform that pivots. Origin sits at the hitch, so it swings correctly.")]
    [SerializeField] private string cabChildName   = "Tractor";
    [Tooltip("Most the cab can crank away from the trailer body, in degrees. ~30° simulates the trailer turn while backing.")]
    [SerializeField] private float maxCabSteer     = 30f;
    [Tooltip("Maps how fast the body is turning (deg/sec) to cab steer angle while driving forward.")]
    [SerializeField] private float cabSteerGain    = 0.22f;
    [Tooltip("How quickly the cab swings toward its target steer angle, deg/sec.")]
    [SerializeField] private float cabSteerSlew    = 140f;

    [Header("Cab steering — reversing into dock")]
    [Tooltip("Cab steer gain used ONLY while backing into the dock. Higher keeps the tractor visibly cranked (~30°) while it tucks in.")]
    [SerializeField] private float reverseCabSteerGain = 1.6f;
    [Tooltip("Invert the cab crank direction while reversing — a real tractor steers opposite the trailer's swing when backing.")]
    [SerializeField] private bool  invertCabSteerWhenReversing = true;

    [Header("Unload & exit")]
    [SerializeField] private float unloadDuration = 7f;
    [SerializeField] private float exitShrinkTime = 1.2f;

    [Header("Trailer Doors")]
    [SerializeField] private float doorOpenSpeed = 150f;

    [Header("State (read-only in play)")]
    [SerializeField] private TruckState _state = TruckState.Idle;

    // ── Route waypoints (world positions, injected by TruckYardManager) ──────────
    private DockSlot        _dock;
    private GuardController  _guard;
    private Vector3?         _gateStop;          // guard inspection
    private Vector3?         _gateEnterNoTurn;   // Xform 1
    private Vector3?         _gateLeaveNoTurn;   // Xform 5
    private Vector3?         _exitWaypoint;      // Exit point
    private System.Action    _onExited;
    // Xform 2 (_drApproach-DepartPoint) and Xform 3 (_drBackup) are computed
    // per-door from DockSlot offsets — see ApproachPoint() / BackupPoint().

    private float       _stateTimer;
    private float       _groundY;
    private Vector3     _currentTarget;
    private Transform   _driverDoor;
    private Transform   _passDoor;
    private bool        _doorsOpen;
    private bool        _useBezier;

    // Cab steering (articulated tractor)
    private Transform   _cab;
    private Quaternion  _cabRest;
    private float       _cabYaw;
    private float       _prevYaw;
    private bool        _cabInit;

    // Bézier segments (forward smoothing legs and the reverse into the door)
    private Vector3 _bzP0, _bzP1, _bzP2, _bzP3;
    private float   _bzT;
    private float   _bzArcLen;

    // Straight reverse (Xform 3 → beginBackupTurn) with gradual yaw
    private Vector3    _revStraightStart, _revStraightTarget;
    private float      _revStraightLen;
    private Quaternion _revFromRot, _revToRot;

    public TruckState State => _state;

    // ── Gate queue ───────────────────────────────────────────────────────────────
    private bool _isFront;       // this truck holds slot 0 (the gate) and may be inspected
    private bool _clearedGate;   // guard waved it through — it's heading into the yard

    /// <summary>True once the truck has passed the guard and left the gate queue.</summary>
    public bool HasClearedGate => _clearedGate;

    /// <summary>Fired once when the guard clears this truck and it leaves the queue.</summary>
    public event System.Action OnClearedGate;

    private void Awake()
    {
        _groundY = transform.position.y;
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        _driverDoor = FindDeepChild(transform, "TrailerDoor.Driver");
        if (_driverDoor == null) _driverDoor = FindDeepChild(transform, "TrailerDoor");

        _passDoor = FindDeepChild(transform, "TrailerDoor.Pass");
        if (_passDoor == null) _passDoor = FindDeepChild(transform, "TrailerDoor.001");

        _cab = FindDeepChild(transform, cabChildName);
        if (_cab != null) _cabRest = _cab.localRotation;
        else if (articulateCab)
            Debug.LogWarning($"[TruckController] Cab child '{cabChildName}' not found — cab steering disabled.");
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }

    // ── Setup ───────────────────────────────────────────────────────────────────
    public void Init(Vector3? gateStop, Vector3? gateEnterNoTurn, Vector3? gateLeaveNoTurn,
                     Vector3? exitWaypoint, GuardController guard, System.Action onExited)
    {
        _gateStop        = gateStop;
        _gateEnterNoTurn = gateEnterNoTurn;
        _gateLeaveNoTurn = gateLeaveNoTurn;
        _exitWaypoint    = exitWaypoint;
        _guard           = guard;
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

        Vector3 firstTarget = _gateStop ?? ApproachPoint();

        // Snap facing before first Update — no visible first-frame spin.
        Vector3 dir = firstTarget - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);

        if (_gateStop.HasValue)
        {
            // Wait in the gate queue. The yard manager assigns our slot via SetQueueSlot().
            _state = TruckState.Queuing;
            _currentTarget = transform.position;
            _useBezier = false;
        }
        else
            GuardClearedToEnter(); // no gate configured — skip straight into the yard
    }

    /// <summary>
    /// Manager-driven gate-queue slot. <paramref name="isFront"/> = this truck holds the
    /// gate (slot 0) and will be inspected once it arrives. Ignored once past the gate.
    /// </summary>
    public void SetQueueSlot(Vector3 slotPos, bool isFront)
    {
        if (_state != TruckState.Queuing) return;
        _currentTarget = slotPos;
        _isFront = isFront;
        _useBezier = false;
    }

    public void ForceDeparture()
    {
        if (_state == TruckState.Docked) BeginDeparture();
    }

    // ── Route points (computed per-door from DockSlot offsets) ───────────────────
    private Vector3 ApproachPoint() => _dock.ApproachDepartPoint;  // Xform 2
    private Vector3 BackupPoint()   => _dock.BackupPoint;          // Xform 3

    // ── Main loop ────────────────────────────────────────────────────────────────
    private void Update()
    {
        UpdateDoors();
        UpdateCabSteering();

        switch (_state)
        {
            case TruckState.Queuing:
                // Drive to our queue slot and idle. Only the front truck (slot 0)
                // triggers the guard inspection once it reaches the gate.
                if (DriveToward(_currentTarget) && _isFront) BeginGuardCheck();
                break;

            case TruckState.GuardCheck:
                // Waiting for GuardClearedToEnter() callback from GuardController.
                break;

            case TruckState.ToEnterNoTurn:
                // Xform 1 — roll through without stopping, straight on to Xform 2.
                if (DriveToward(_currentTarget))
                    SetTarget(TruckState.ToApproach, ApproachPoint());
                break;

            case TruckState.ToApproach:
                // Xform 2 — straight, no stop, then a rounded Bézier corner to Xform 3.
                if (DriveToward(_currentTarget))
                    BeginApproachToBackupCurve();
                break;

            case TruckState.ToBackup:
                // Xform 2 → Xform 3 (rounded corner). On arrival, face AWAY from the
                // door along the Xform 3 → beginBackupTurn line, then stop and wait.
                if (DriveToward(_currentTarget))
                {
                    Vector3 awayDir = BackupPoint() - _dock.BeginBackupTurnPoint;
                    awayDir.y = 0f;
                    if (awayDir.sqrMagnitude > 0.01f)
                        transform.rotation = Quaternion.LookRotation(awayDir.normalized);

                    _state      = TruckState.WaitingAtBackup;
                    _stateTimer = backupWaitDuration;
                }
                break;

            case TruckState.WaitingAtBackup:
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f) BeginStraightReverse();
                break;

            case TruckState.Reversing:
                StepStraightReverse();
                break;

            case TruckState.ReversingToDock:
                StepFinalReverse();
                break;

            case TruckState.Docked:
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f) BeginDeparture();
                break;

            case TruckState.DepartToApproach:
                // Pull out forward to Xform 2, then on to Xform 5 without stopping.
                if (DriveToward(_currentTarget))
                {
                    if (_gateLeaveNoTurn.HasValue)
                    {
                        Vector3 afterGate = _exitWaypoint ?? _gateLeaveNoTurn.Value;
                        SetTargetCurved(TruckState.ToLeaveNoTurn, _gateLeaveNoTurn.Value,
                                        afterGate - _gateLeaveNoTurn.Value);
                    }
                    else
                        StartExiting();
                }
                break;

            case TruckState.ToLeaveNoTurn:
                // Xform 5 — roll through without stopping, on to the exit.
                if (DriveToward(_currentTarget)) StartExiting();
                break;

            case TruckState.ToExit:
                if (DriveToward(_currentTarget))
                {
                    _state = TruckState.Idle;
                    StartCoroutine(ShrinkAndDestroy(exitShrinkTime));
                }
                break;
        }
    }

    // ── Cab steering (articulated tractor) ────────────────────────────────────────
    private void UpdateCabSteering()
    {
        if (!articulateCab || _cab == null) return;

        float curYaw = transform.eulerAngles.y;

        if (!_cabInit)
        {
            _prevYaw = curYaw;
            _cabInit = true;
            return;
        }

        float dt      = Mathf.Max(Time.deltaTime, 1e-4f);
        float yawRate = Mathf.DeltaAngle(_prevYaw, curYaw) / dt;
        _prevYaw      = curYaw;

        bool  reversing = _state == TruckState.Reversing || _state == TruckState.ReversingToDock;
        float gain      = reversing ? reverseCabSteerGain : cabSteerGain;
        float sign      = (reversing && invertCabSteerWhenReversing) ? -1f : 1f;

        float steerTarget = Mathf.Clamp(yawRate * gain * sign, -maxCabSteer, maxCabSteer);
        _cabYaw           = Mathf.MoveTowards(_cabYaw, steerTarget, cabSteerSlew * dt);

        _cab.localRotation = Quaternion.Euler(0f, _cabYaw, 0f) * _cabRest;
    }

    // ── Forward movement (straight or Bézier) ─────────────────────────────────────
    private bool DriveToward(Vector3 target)
    {
        if (_useBezier)
        {
            _bzT += (driveSpeed / Mathf.Max(_bzArcLen, 0.1f)) * Time.deltaTime;

            if (_bzT >= 1f)
            {
                transform.position = _bzP3;
                _useBezier = false;
                return true;
            }

            Vector3 pos = EvalBezier(_bzP0, _bzP1, _bzP2, _bzP3, _bzT);
            pos.y = _groundY;
            transform.position = pos;

            Vector3 tangent = EvalBezierTangent(_bzP0, _bzP1, _bzP2, _bzP3, _bzT);
            tangent.y = 0f;
            if (tangent.sqrMagnitude > 0.001f)
            {
                Quaternion desired = Quaternion.LookRotation(tangent.normalized);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, desired, driveTurnSpeed * 1.5f * Time.deltaTime);
            }
            return false;
        }

        Vector3 flat     = new Vector3(target.x, _groundY, target.z);
        Vector3 toTarget = flat - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude < arrivedThreshold)
        {
            transform.position = flat;
            return true;
        }

        Quaternion desiredLinear = Quaternion.LookRotation(toTarget.normalized);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, desiredLinear, driveTurnSpeed * Time.deltaTime);

        transform.position = Vector3.MoveTowards(
            transform.position, flat, driveSpeed * Time.deltaTime);

        return false;
    }

    private void SetupBezierForward(Vector3 endPos, Vector3 endForward, float tension)
    {
        _bzP0 = transform.position;
        _bzP3 = new Vector3(endPos.x, _groundY, endPos.z);

        float chord = Vector3.Distance(_bzP0, _bzP3);
        float t = chord * tension;

        _bzP1 = _bzP0 + transform.forward * t;
        _bzP2 = _bzP3 - endForward.normalized * t;

        _bzT = 0f;
        _bzArcLen = chord * 1.4f;
        _useBezier = true;
    }

    // ── Backup maneuver ───────────────────────────────────────────────────────────
    // Xform 2 → Xform 3: rounded Bézier corner, control at (Xform2.x, Xform3.z).
    private void BeginApproachToBackupCurve()
    {
        Vector3 x2     = ApproachPoint();
        Vector3 x3     = BackupPoint();
        Vector3 corner = new Vector3(x2.x, _groundY, x3.z);   // (ApproachDepart.x, Backup.z)

        _bzP0 = new Vector3(transform.position.x, _groundY, transform.position.z);
        _bzP1 = corner;
        _bzP2 = corner;   // both controls at the corner → a clean rounded right-angle
        _bzP3 = new Vector3(x3.x, _groundY, x3.z);
        _bzT  = 0f;
        _bzArcLen = (Vector3.Distance(_bzP0, corner) + Vector3.Distance(corner, _bzP3)) * 0.9f;
        _useBezier = true;

        _state         = TruckState.ToBackup;
        _currentTarget = _bzP3;
    }

    // Xform 3 → beginBackupTurn: STRAIGHT reverse path while slowly yawing the mesh
    // to the door-aligned heading (the door-relative "+X" target).
    private void BeginStraightReverse()
    {
        _revStraightStart  = new Vector3(transform.position.x, _groundY, transform.position.z);
        _revStraightTarget = new Vector3(_dock.BeginBackupTurnPoint.x, _groundY, _dock.BeginBackupTurnPoint.z);
        _revStraightLen    = Mathf.Max(0.1f, Vector3.Distance(_revStraightStart, _revStraightTarget));
        _revFromRot        = transform.rotation;
        _revToRot          = _dock.DockRotation;   // door-relative, aligned to back in
        _state             = TruckState.Reversing;
    }

    private void StepStraightReverse()
    {
        transform.position = Vector3.MoveTowards(transform.position, _revStraightTarget, reverseSpeed * Time.deltaTime);

        float t = Mathf.Clamp01(Vector3.Distance(_revStraightStart, transform.position) / _revStraightLen);
        transform.rotation = Quaternion.Slerp(_revFromRot, _revToRot, t);   // slow yaw, straight path

        // Blend into the final curve ~half-a-truck before reaching beginBackupTurn
        // so the straight reverse and the curve join as one smooth S (no kink). The
        // truck is only partly yawed here, leaving real angle for the curve to resolve.
        float lead = Mathf.Min(turnLeadDistance, _revStraightLen * 0.9f);
        if (Vector3.Distance(transform.position, _revStraightTarget) <= Mathf.Max(arrivedThreshold, lead))
            BeginFinalReverse();
    }

    // beginBackupTurn → dock: Bézier reverse into the door, ending at the dock
    // position (dockOffset standoff). The dock direction shapes the curve.
    private void BeginFinalReverse()
    {
        var p0   = new Vector3(transform.position.x, _groundY, transform.position.z);
        var dock = new Vector3(_dock.DockPosition.x, _groundY, _dock.DockPosition.z);

        var dockFwd = _dock.DockRotation * Vector3.forward; dockFwd.y = 0f; dockFwd.Normalize();
        var back    = -transform.forward; back.y = 0f; back.Normalize();

        float chord   = Vector3.Distance(p0, dock);
        float tension = chord * reverseArcTension;

        _bzP0 = p0;
        _bzP1 = p0   + back    * tension;
        _bzP2 = dock + dockFwd * tension;
        _bzP3 = dock;
        _bzT  = 0f;
        _bzArcLen = chord * 1.5f;

        _state = TruckState.ReversingToDock;
    }

    private void StepFinalReverse()
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

        Vector3 pos = EvalBezier(_bzP0, _bzP1, _bzP2, _bzP3, _bzT);
        pos.y = _groundY;
        transform.position = pos;

        // Mesh faces -tangent (cab points away from travel = reversing).
        Vector3 tangent = EvalBezierTangent(_bzP0, _bzP1, _bzP2, _bzP3, _bzT);
        tangent.y = 0f;
        if (tangent.sqrMagnitude > 0.001f)
        {
            Quaternion desired = Quaternion.LookRotation(-tangent.normalized);
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

    // ── Phase transitions ─────────────────────────────────────────────────────────
    private void SetTarget(TruckState next, Vector3 destination)
    {
        _state         = next;
        _currentTarget = destination;
        _useBezier     = false;
    }

    private void SetTargetCurved(TruckState next, Vector3 destination, Vector3 endForward)
    {
        _state         = next;
        _currentTarget = destination;

        endForward.y = 0f;
        if (endForward.sqrMagnitude < 0.0001f)
        {
            _useBezier = false;
            return;
        }
        SetupBezierForward(destination, endForward.normalized, forwardDriveTension);
    }

    private void BeginGuardCheck()
    {
        _state = TruckState.GuardCheck;
        if (_guard != null)
            _guard.BeginInspection(this, GuardClearedToEnter);
        else
            GuardClearedToEnter();
    }

    public void GuardClearedToEnter()
    {
        // Leave the gate queue — the manager advances everyone behind us.
        _clearedGate = true;
        OnClearedGate?.Invoke();

        // Xform 1: roll through the gate without stopping (straight leg).
        if (_gateEnterNoTurn.HasValue)
            SetTarget(TruckState.ToEnterNoTurn, _gateEnterNoTurn.Value);
        else
            SetTarget(TruckState.ToApproach, ApproachPoint());
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
        _dock.Release();

        // Pull out forward to Xform 2, easing toward the exit direction.
        Vector3 ap   = ApproachPoint();
        Vector3 next = _gateLeaveNoTurn ?? _exitWaypoint ?? ap;
        SetTargetCurved(TruckState.DepartToApproach, ap, next - ap);
    }

    private void StartExiting()
    {
        if (_exitWaypoint.HasValue)
            SetTarget(TruckState.ToExit, _exitWaypoint.Value);
        else
        {
            _state = TruckState.Idle;
            StartCoroutine(ShrinkAndDestroy(exitShrinkTime));
        }
    }

    // ── Trailer doors ─────────────────────────────────────────────────────────────
    /// <summary>Passenger-side rear trailer door (the guard faces this during inspection).</summary>
    public Transform PassengerDoor => _passDoor;

    public void OpenTrailerDoors()  { _doorsOpen = true; }
    public void CloseTrailerDoors() { _doorsOpen = false; }

    private void UpdateDoors()
    {
        if (_driverDoor == null || _passDoor == null) return;

        float targetDriver = _doorsOpen ? 65f : 0f;
        float targetPass   = _doorsOpen ? -65f : 0f;

        Quaternion drRot = Quaternion.Euler(0, targetDriver, 0);
        Quaternion paRot = Quaternion.Euler(0, targetPass, 0);

        _driverDoor.localRotation = Quaternion.RotateTowards(_driverDoor.localRotation, drRot, doorOpenSpeed * Time.deltaTime);
        _passDoor.localRotation   = Quaternion.RotateTowards(_passDoor.localRotation, paRot, doorOpenSpeed * Time.deltaTime);
    }

    // ── Teardown ──────────────────────────────────────────────────────────────────
    private void OnDestroy()
    {
        if (_dock != null)
        {
            _dock.Release();
            _dock = null;
        }
        if (_onExited != null)
        {
            _onExited.Invoke();
            _onExited = null;
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
