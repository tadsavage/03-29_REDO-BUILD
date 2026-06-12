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
        Approaching, Looping, PullingPast,
        Aligning, Reversing,
        Docked,
        DepartingDock,
        LeavingYard,
        Exiting
    }

    [Header("Driving")]
    [SerializeField] private float driveSpeed       = 2.5f;
    [SerializeField] private float driveTurnSpeed   = 180f;
    [SerializeField] private float arrivedThreshold = 0.5f;

    [Header("Align (spin before reversing)")]
    [SerializeField] private float alignTurnSpeed      = 90f;
    [SerializeField] private float alignAngleTolerance = 1f;

    [Header("Reversing — Bezier arc")]
    [SerializeField] private float reverseSpeed = 3f;
    [Tooltip("0 = nearly straight, 1 = wide sweeping curve. 0.45 is a natural truck arc.")]
    [SerializeField] private float reverseArcTension = 0.45f;

    [Header("Trailer articulation")]
    [Tooltip("How much the Trailer child swings on local Y relative to the tractor while backing. 0.3–0.5 looks subtle and realistic.")]
    [SerializeField] private float trailerArticulationScale = 0.4f;

    [Header("Cab steering (tractor yaws at the hitch)")]
    [Tooltip("Turn the cab on its Y axis so it leads into curves, like a real tractor pivoting at the fifth wheel. Pure yaw — never touches X/Z.")]
    [SerializeField] private bool  articulateCab   = true;
    [Tooltip("Name of the cab child transform that pivots. Origin sits at the hitch, so it swings correctly.")]
    [SerializeField] private string cabChildName   = "Tractor";
    [Tooltip("Most the cab can crank away from the trailer body, in degrees. Real semis sit around 30–40°.")]
    [SerializeField] private float maxCabSteer     = 35f;
    [Tooltip("Maps how fast the body is turning (deg/sec) to cab steer angle. Higher = cab cranks harder in curves.")]
    [SerializeField] private float cabSteerGain    = 0.22f;
    [Tooltip("How quickly the cab swings toward its target steer angle, deg/sec. Lower = lazier, heavier feel.")]
    [SerializeField] private float cabSteerSlew    = 140f;

    [Header("Cab steering — reversing into dock")]
    [Tooltip("Cab steer gain used ONLY while backing into the dock. The body barely yaws during the reverse arc, so this is normally much higher than the forward gain to keep the tractor visibly cranked while it tucks in.")]
    [SerializeField] private float reverseCabSteerGain = 1.6f;
    [Tooltip("Invert the cab crank direction while reversing — a real tractor steers opposite to the trailer's swing when backing. Turn off if the cab leans the wrong way.")]
    [SerializeField] private bool  invertCabSteerWhenReversing = true;

    [Header("Unload & exit")]
    [SerializeField] private float unloadDuration = 7f;
    [SerializeField] private float exitShrinkTime = 1.2f;

    [Header("Trailer Doors")]
    [SerializeField] private float doorOpenSpeed = 150f;

    [Header("State (read-only in play)")]
    [SerializeField] private TruckState _state = TruckState.Idle;

    [Header("Forward Driving — Bezier")]
    [SerializeField] private float forwardDriveTension = 0.45f;

    private DockSlot        _dock;
    private GuardController _guard;
    private Vector3?        _gateStop;
    private Vector3?      _gateEnterNoTurn;
    private Vector3?      _gateLeaveNoTurn;
    private Vector3?      _exitWaypoint;
    private System.Action _onExited;
    private Quaternion    _targetAlignRot;
    private float         _stateTimer;
    private float         _groundY;
    private Vector3       _currentTarget;
    private Transform     _driverDoor;
    private Transform     _passDoor;
    private bool          _doorsOpen;
    private bool          _useBezier;

    // Cab steering (articulated tractor)
    private Transform     _cab;
    private Quaternion    _cabRest;
    private float         _cabYaw;
    private float         _prevYaw;
    private bool          _cabInit;

    // Bezier segments (shared for reverse and forward smoothing)
    private Vector3 _bzP0, _bzP1, _bzP2, _bzP3;
    private float   _bzT;
    private float   _bzArcLen;

    public TruckState State => _state;

    private void Awake()
    {
        _groundY = transform.position.y;
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        // Find trailer doors
        _driverDoor = FindDeepChild(transform, "TrailerDoor.Driver");
        if (_driverDoor == null) _driverDoor = FindDeepChild(transform, "TrailerDoor");
        
        _passDoor = FindDeepChild(transform, "TrailerDoor.Pass");
        if (_passDoor == null) _passDoor = FindDeepChild(transform, "TrailerDoor.001");

        // Cab (tractor) — pivots on Y at the hitch to steer into curves
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

        Vector3 firstTarget = _gateStop.HasValue ? _gateStop.Value : _dock.ApproachPoint;

        // Snap facing before first Update — no visible first-frame spin
        Vector3 dir = firstTarget - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);

        if (_gateStop.HasValue)
            SetTarget(TruckState.WaitingAtGate, _gateStop.Value);
        else
            SetTargetCurved(TruckState.Approaching, firstTarget,
                            _dock.PullPastPoint - firstTarget);
    }

    public void ForceDeparture()
    {
        if (_state == TruckState.Docked) BeginDeparture();
    }

    private void Update()
    {
        UpdateDoors();
        UpdateCabSteering();

        switch (_state)
        {
            case TruckState.WaitingAtGate:
                if (DriveToward(_currentTarget)) BeginGuardCheck();
                break;

            case TruckState.GuardCheck:
                // Waiting for GuardClearedToEnter() callback from GuardController.
                break;

            case TruckState.EnteringYard:
                if (DriveToward(_currentTarget))
                {
                    SetTarget(TruckState.Approaching, _dock.ApproachPoint);
                }
                break;

            case TruckState.Approaching:
                {
                    float distToApproach = Vector3.Distance(transform.position, _dock.ApproachPoint);
                    if (distToApproach <= 4.0f)
                    {
                        BeginLoop();
                    }
                    else
                    {
                        DriveToward(_currentTarget);
                    }
                }
                break;

            case TruckState.Looping:
                if (DriveToward(_currentTarget))
                {
                    Vector3 ap = _dock.ApproachPoint;
                    Vector3 pp = _dock.PullPastPoint;
                    Vector3 dt = _dock.DockPosition;
                    Vector3 fwd = _dock.DockRotation * Vector3.forward;

                    Vector3 purpleLineDir = (pp - dt).normalized;
                    Vector3 perpDir = Vector3.Cross(purpleLineDir, Vector3.up).normalized;
                    if (Vector3.Dot(perpDir, fwd) < 0f)
                    {
                        perpDir = -perpDir;
                    }

                    Vector3 loopEndPoint = pp + perpDir * 2.5f;

                    SetTargetCurved(TruckState.PullingPast, loopEndPoint, perpDir);
                }
                break;

            case TruckState.PullingPast:
                if (DriveToward(_currentTarget)) BeginReverse();
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
                    {
                        Vector3 afterGate = _exitWaypoint ?? _gateLeaveNoTurn.Value;
                        SetTargetCurved(TruckState.LeavingYard, _gateLeaveNoTurn.Value,
                                        afterGate - _gateLeaveNoTurn.Value);
                    }
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

    // ── Cab steering (articulated tractor) ────────────────────────────────────
    //
    // The cab pivots on Y at the hitch (its origin) so it leads into curves like a
    // real tractor at the fifth wheel. The steer angle is driven by how fast the
    // truck *body* is yawing this frame: tight curve → big crank, straight → 0.
    // Works for the forward arc, the align spin, and the reverse-into-dock arc with
    // no special-casing. Only ever writes local Y — X/Z come from the cab's rest
    // pose every frame, so they can never drift.
    private void UpdateCabSteering()
    {
        if (!articulateCab || _cab == null) return;

        float curYaw = transform.eulerAngles.y;

        // Defer the first sample so the spawn snap doesn't register as a yaw spike.
        if (!_cabInit)
        {
            _prevYaw = curYaw;
            _cabInit = true;
            return;
        }

        float dt      = Mathf.Max(Time.deltaTime, 1e-4f);
        float yawRate = Mathf.DeltaAngle(_prevYaw, curYaw) / dt;   // deg/sec, signed
        _prevYaw      = curYaw;

        // Reversing barely yaws the body and inverts the cab/trailer relationship,
        // so it gets its own gain (usually higher) and an optional sign flip.
        bool  reversing = _state == TruckState.Reversing;
        float gain      = reversing ? reverseCabSteerGain : cabSteerGain;
        float sign      = (reversing && invertCabSteerWhenReversing) ? -1f : 1f;

        float steerTarget = Mathf.Clamp(yawRate * gain * sign, -maxCabSteer, maxCabSteer);
        _cabYaw           = Mathf.MoveTowards(_cabYaw, steerTarget, cabSteerSlew * dt);

        // Pure-Y crank applied on top of the cab's rest orientation.
        _cab.localRotation = Quaternion.Euler(0f, _cabYaw, 0f) * _cabRest;
    }

    // ── Direct forward movement ───────────────────────────────────────────────

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

            // Position follows the cubic Bezier
            Vector3 pos = EvalBezier(_bzP0, _bzP1, _bzP2, _bzP3, _bzT);
            pos.y = _groundY;
            transform.position = pos;

            // Rotation follows tangent (forward)
            Vector3 tangent = EvalBezierTangent(_bzP0, _bzP1, _bzP2, _bzP3, _bzT);
            tangent.y = 0f;
            if (tangent.sqrMagnitude > 0.001f)
            {
                Quaternion desired = Quaternion.LookRotation(tangent.normalized);
                // Faster rotation during smoothing looks more natural for a heavy vehicle
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

    // ── Bezier forward setup ──────────────────────────────────────────────────

    private void SetupBezierForward(Vector3 endPos, Vector3 endForward, float tension)
    {
        _bzP0 = transform.position;
        _bzP3 = new Vector3(endPos.x, _groundY, endPos.z);
        
        float chord = Vector3.Distance(_bzP0, _bzP3);
        float t = chord * tension;

        // Start tangent: straight out from current heading
        _bzP1 = _bzP0 + transform.forward * t;
        // End tangent: arriving along the target forward
        _bzP2 = _bzP3 - endForward.normalized * t;
        
        _bzT = 0f;
        _bzArcLen = chord * 1.4f; 
        _useBezier = true;
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
        _useBezier     = false;   // straight leg — clear any leftover arc
    }

    /// <summary>
    /// Drive a forward leg as a planned Bezier curve that arrives at <paramref name="destination"/>
    /// already pointing along <paramref name="endForward"/> (the direction of the NEXT leg, or the
    /// dock-facing direction). This is what lets the truck ease into corners and straighten out
    /// instead of pivoting in place. Falls back to a straight leg if the look-ahead is degenerate.
    /// </summary>
    private void SetTargetCurved(TruckState next, Vector3 destination, Vector3 endForward)
    {
        _state         = next;
        _currentTarget = destination;

        endForward.y = 0f;
        if (endForward.sqrMagnitude < 0.0001f)
        {
            _useBezier = false;   // nothing meaningful to aim at — just drive straight
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

    public void GuardClearedToEnter()
{
        if (_gateEnterNoTurn.HasValue)
            SetTarget(TruckState.EnteringYard, _gateEnterNoTurn.Value);
        else
            SetTargetCurved(TruckState.Approaching, _dock.ApproachPoint,
                            _dock.PullPastPoint - _dock.ApproachPoint);
    }

    private void BeginLoop()
    {
        _state = TruckState.Looping;

        Vector3 ap = _dock.ApproachPoint;
        Vector3 pp = _dock.PullPastPoint;
        Vector3 fwd = _dock.DockRotation * Vector3.forward;
        Vector3 sideDir = (pp - ap).normalized;

        // Backside loop point: behind approach point, and slightly opposite to the pull-past side
        Vector3 loopBackPoint = ap + fwd * 4.5f - sideDir * 2.0f;

        // Target forward direction at the loop back point: diagonal towards the pull-past side
        Vector3 loopBackForward = (sideDir + fwd * 0.5f).normalized;

        SetTargetCurved(TruckState.Looping, loopBackPoint, loopBackForward);
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

        // Pull out of the dock and curve toward wherever we leave through next.
        Vector3 nextOut = _gateLeaveNoTurn ?? _exitWaypoint ?? _dock.PullPastPoint;
        SetTargetCurved(TruckState.DepartingDock, _dock.PullPastPoint,
                        nextOut - _dock.PullPastPoint);
    }

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
