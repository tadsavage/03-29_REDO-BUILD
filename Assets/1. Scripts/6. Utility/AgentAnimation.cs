using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Drives the MaleStaff animator and agent heading. Owns visual rotation
/// (UpdateRotation=false on the NavMeshAgent); writes it in LateUpdate so it
/// wins the ordering battle against NavMeshAgent's internal PreLateUpdate step.
///
/// Animation rules (highest priority first):
///   1. Climbing / JumpingDown  — override everything
///   2. Waving                  — agent is stuck with no waypoints
///   3. Walk + Turn             — agent is mid-path
///   4. Idle                    — everything else
/// </summary>
public class AgentAnimation : MonoBehaviour
{
    private NavMeshAgent        _agent;
    private Animator            _animator;
    private AiNavigation        _navigation;
    private Rigidbody           _rb;
    private NoWaypointIndicator _indicator;

    [Header("Movement")]
    [Tooltip("Base walk speed set on the NavMeshAgent.")]
    [SerializeField] private float walkSpeed         = 2f;
    [Tooltip("Degrees per second for visual heading rotation (higher = snappier turns).")]
    [SerializeField] private float turnSpeed         = 480f;
    [Tooltip("Seconds the agent pauses at a waypoint before resuming.")]
    [SerializeField] private float idleDelay         = 1.5f;
    [Tooltip("NavMeshAgent stopping distance (metres from waypoint).")]
    [SerializeField] private float waypointThreshold = 0.5f;

    [Header("Turn")]
    [Tooltip("Degrees between agent forward and desired heading that triggers a turn animation.")]
    [SerializeField] private float angleThreshold    = 40f;
    [Tooltip("Fraction of walkSpeed applied while turning. 0 = stop, 1 = full speed.")]
    [SerializeField, Range(0f, 1f)] private float speedAdj = 0.55f;
    [Tooltip("Distance to next path corner at which pre-turn slow-down begins.")]
    [SerializeField] private float turnLookAheadDist = 2f;

    [Header("Waving")]
    [Tooltip("Seconds agent idles with no waypoints before the wave animation starts.")]
    [SerializeField] private float waveDelay = 2f;

    // ── Runtime state ──────────────────────────────────────────────────────────
    private bool  _isWaiting;
    private bool  _everHadPath;
    private float _waveTimer;

    // Rotation computed in Update, committed in LateUpdate so it fires after
    // NavMeshAgent's PreLateUpdate (which would otherwise reset it to zero).
    private Quaternion _pendingRotation;
    private bool       _hasPendingRotation;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void Start()
    {
        _agent      = GetComponent<NavMeshAgent>();
        _animator   = GetComponent<Animator>();
        _navigation = GetComponent<AiNavigation>();
        _indicator  = GetComponent<NoWaypointIndicator>();
        _rb         = GetComponent<Rigidbody>();

        _pendingRotation = transform.rotation;

        _agent.speed            = walkSpeed;
        _agent.angularSpeed     = turnSpeed;   // used for internal NavMesh path planning only
        _agent.acceleration     = 12f;
        _agent.stoppingDistance = waypointThreshold;
        _agent.updateRotation   = false;       // this script owns heading
    }

    void Update()
    {
        if (_animator == null) return;

        if (!_agent.isOnNavMesh)
        {
            SetAllBools(false);
            return;
        }

        if (_agent.hasPath || _agent.velocity.sqrMagnitude > 0.01f)
            _everHadPath = true;

        // ── Arrival check ──────────────────────────────────────────────────────
        bool arrived = !_agent.pathPending
                    && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f
                    && _agent.pathStatus == NavMeshPathStatus.PathComplete;

        if (arrived && !_isWaiting && _everHadPath)
            StartCoroutine(WaitAtWaypointRoutine());

        // ── Visual heading ─────────────────────────────────────────────────────
        if (!_isWaiting)
            UpdateHeading(arrived);

        // ── Priority 1: Climbing / JumpingDown ────────────────────────────────
        bool climbing    = _navigation != null && _navigation.IsTraversingLedgeUp;
        bool jumpingDown = _navigation != null && _navigation.IsTraversingLedgeDown;
        if (climbing || jumpingDown)
        {
            SetAllBools(false);
            _animator.SetBool("IsClimbing",    climbing);
            _animator.SetBool("IsJumpingDown", jumpingDown);
            return;
        }
        _animator.SetBool("IsClimbing",    false);
        _animator.SetBool("IsJumpingDown", false);

        // ── Priority 2: Waving ─────────────────────────────────────────────────
        bool indicatorOn = _indicator != null && _indicator.IsShowingIndicator;
        _waveTimer       = indicatorOn ? _waveTimer + Time.deltaTime : 0f;
        bool waving      = indicatorOn && _waveTimer >= waveDelay;
        _animator.SetBool("IsWaving", waving);
        if (waving)
        {
            _animator.SetBool("IsWalking",      false);
            _animator.SetBool("IsTurningLeft",  false);
            _animator.SetBool("IsTurningRight", false);
            return;
        }

        // ── Priority 3 & 4: Walk + Turn / Idle ────────────────────────────────
        // IsWalking is driven by remaining path distance, NOT velocity.
        // This makes the walk animation stop on the exact frame the agent arrives
        // rather than ~0.3s later when velocity finally winds down to zero.
        bool isWalking = !_isWaiting
                      && _agent.hasPath
                      && !_agent.pathPending
                      && _agent.remainingDistance > _agent.stoppingDistance + 0.05f;

        _animator.SetBool("IsWalking", isWalking);

        // Turn bools: compare desired heading (path intent) to current facing.
        // desiredVelocity is slightly ahead of actual movement, so turn animations
        // lead the physical turn — which is the correct feel.
        bool turningLeft  = false;
        bool turningRight = false;

        if (isWalking && _agent.velocity.sqrMagnitude > 0.04f)
        {
            var flat = new Vector3(_agent.desiredVelocity.x, 0f, _agent.desiredVelocity.z);
            if (flat.sqrMagnitude > 0.01f)
            {
                float angle = Vector3.SignedAngle(transform.forward, flat.normalized, Vector3.up);
                if (Mathf.Abs(angle) >= angleThreshold)
                {
                    turningLeft  = angle < 0f;
                    turningRight = angle > 0f;
                }
            }
        }

        _animator.SetBool("IsTurningLeft",  turningLeft);
        _animator.SetBool("IsTurningRight", turningRight);

        // ── Speed ──────────────────────────────────────────────────────────────
        UpdateSpeed(isWalking, turningLeft || turningRight);
    }

    void LateUpdate()
    {
        if (!_hasPendingRotation) return;
        transform.rotation = _pendingRotation;
        if (_rb != null) _rb.MoveRotation(_pendingRotation);
    }

    // ── Heading ────────────────────────────────────────────────────────────────

    private void UpdateHeading(bool arrived)
    {
        Vector3 vel = _agent.velocity;
        vel.y = 0f;

        if (vel.sqrMagnitude > 0.04f)
        {
            // Moving: face actual velocity direction — stable, no oscillation.
            _pendingRotation    = Quaternion.RotateTowards(_pendingRotation, Quaternion.LookRotation(vel.normalized), turnSpeed * Time.deltaTime);
            _hasPendingRotation = true;
        }
        else if (!arrived && _agent.hasPath && !_agent.pathPending)
        {
            // Standing still with a valid destination: pre-rotate toward it so
            // the agent already faces the right direction when walking starts.
            Vector3 dir = _agent.steeringTarget - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                _pendingRotation    = Quaternion.RotateTowards(_pendingRotation, Quaternion.LookRotation(dir.normalized), turnSpeed * Time.deltaTime);
                _hasPendingRotation = true;
            }
        }
    }

    // ── Speed ──────────────────────────────────────────────────────────────────

    private void UpdateSpeed(bool isWalking, bool turning)
    {
        if (!isWalking)
        {
            _agent.speed = walkSpeed;
            return;
        }

        float target = walkSpeed;

        if (turning)
        {
            target = walkSpeed * speedAdj;
        }
        else
        {
            // Pre-slow approaching a sharp path corner.
            var corners = _agent.path.corners;
            if (corners.Length >= 3)
            {
                var p2 = new Vector2(transform.position.x, transform.position.z);
                var p1 = new Vector2(corners[1].x,         corners[1].z);
                if (Vector2.Distance(p2, p1) <= turnLookAheadDist)
                {
                    Vector3 inDir  = new Vector3(corners[1].x - transform.position.x, 0f, corners[1].z - transform.position.z).normalized;
                    Vector3 outDir = new Vector3(corners[2].x - corners[1].x,         0f, corners[2].z - corners[1].z        ).normalized;
                    float ca = Vector3.Angle(inDir, outDir);
                    if (ca >= angleThreshold)
                    {
                        float t = Mathf.Clamp01((ca - angleThreshold) / (180f - angleThreshold));
                        target = Mathf.Lerp(walkSpeed, walkSpeed * speedAdj, t);
                    }
                }
            }
        }

        _agent.speed = Mathf.MoveTowards(_agent.speed, target, walkSpeed * 6f * Time.deltaTime);
    }

    // ── Coroutine ──────────────────────────────────────────────────────────────

    private IEnumerator WaitAtWaypointRoutine()
    {
        _isWaiting   = true;
        _agent.speed = walkSpeed;

        if (_agent.isActiveAndEnabled && _agent.isOnNavMesh)
            _agent.isStopped = true;

        // Request next destination immediately so the path computes during the pause,
        // meaning the agent starts moving instantly when isStopped=false.
        _navigation?.GoToRandomWaypoint();

        yield return new WaitForSeconds(idleDelay);

        if (_agent != null && _agent.isActiveAndEnabled)
            _agent.isStopped = false;

        _isWaiting = false;
    }

    // ── Helper ─────────────────────────────────────────────────────────────────

    private void SetAllBools(bool v)
    {
        _animator.SetBool("IsWalking",      v);
        _animator.SetBool("IsTurningLeft",  v);
        _animator.SetBool("IsTurningRight", v);
        _animator.SetBool("IsWaving",       v);
        _animator.SetBool("IsClimbing",     v);
        _animator.SetBool("IsJumpingDown",  v);
    }
}
