using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class AgentAnimation : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;
    private AiNavigation navigation;
    private Rigidbody _rb;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float turnSpeed = 200f;     // degrees per second
    [SerializeField] private float idleDelay = 1.5f;     // delay at waypoint
    [SerializeField] private float waypointThreshold = 0.5f;

    private bool isWaiting;
    private bool _everHadPath;
    private NoWaypointIndicator _indicator;
    private float _waveDelayTimer;
    [SerializeField] private float waveDelay = 2f;

    // Rotation is computed in Update() but applied in LateUpdate() so it fires
    // after the NavMeshAgent's PreLateUpdate step, which otherwise resets the
    // transform rotation to zero before user LateUpdates run.
    private Quaternion _pendingRotation;
    private bool _hasPendingRotation;

    void Start()
    {
        agent      = GetComponent<NavMeshAgent>();
        animator   = GetComponent<Animator>();
        navigation = GetComponent<AiNavigation>();
        _indicator = GetComponent<NoWaypointIndicator>();
        _rb        = GetComponent<Rigidbody>();

        _pendingRotation = transform.rotation;

        // Setup agent
        agent.speed = walkSpeed;
        agent.angularSpeed = turnSpeed;
        agent.acceleration = 12f;
        agent.stoppingDistance = waypointThreshold;

        // Disable NavMeshAgent auto-rotation — AgentAnimation owns the heading.
        agent.updateRotation = false;
    }

    void Update()
    {
        if (!agent.isOnNavMesh)
        {
            if (animator != null) animator.SetBool("IsWalking", false);
            return;
        }

        // Latch: once we've ever had a path or velocity, we've been navigating.
        if (agent.hasPath || agent.velocity.sqrMagnitude > 0.01f) _everHadPath = true;

        // 1. Flow Control
        bool isAtDestination = !agent.pathPending
            && agent.remainingDistance <= agent.stoppingDistance + 0.1f
            && agent.pathStatus == NavMeshPathStatus.PathComplete;

        if (isAtDestination && !isWaiting && _everHadPath)
        {
            StartCoroutine(WaitAndTurnRoutine());
        }

        // 2. Compute target rotation — stored for LateUpdate application.
        // NavMeshAgent's internal update runs in PreLateUpdate and resets
        // transform.rotation before user LateUpdate, so we must apply rotation
        // there (not here) to win the ordering battle.
        if (!isWaiting)
        {
            Vector3 flatDesired = new Vector3(agent.desiredVelocity.x, 0f, agent.desiredVelocity.z);
            if (flatDesired.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(flatDesired.normalized);
                _pendingRotation = Quaternion.RotateTowards(_pendingRotation, targetRot, turnSpeed * Time.deltaTime);
                _hasPendingRotation = true;
            }
            else if (!isAtDestination && agent.hasPath && !agent.pathPending)
            {
                Vector3 dir = agent.steeringTarget - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    Quaternion destRot = Quaternion.LookRotation(dir.normalized);
                    _pendingRotation = Quaternion.RotateTowards(_pendingRotation, destRot, turnSpeed * Time.deltaTime);
                    _hasPendingRotation = true;
                }
            }
        }

        // 3. Animation Sync
        if (animator != null)
        {
            bool isClimbing    = navigation != null && navigation.IsTraversingLedgeUp;
            bool isJumpingDown = navigation != null && navigation.IsTraversingLedgeDown;

            if (isClimbing || isJumpingDown)
            {
                animator.SetBool("IsClimbing",    isClimbing);
                animator.SetBool("IsJumpingDown", isJumpingDown);
                animator.SetBool("IsWalking",     false);
                animator.SetBool("IsWaving",      false);
            }
            else
            {
                animator.SetBool("IsClimbing",    false);
                animator.SetBool("IsJumpingDown", false);

                bool indicatorVisible = _indicator != null && _indicator.IsShowingIndicator;
                if (indicatorVisible)
                    _waveDelayTimer += Time.deltaTime;
                else
                    _waveDelayTimer = 0f;

                bool isWaving = indicatorVisible && _waveDelayTimer >= waveDelay;
                animator.SetBool("IsWaving", isWaving);

                if (isWaving)
                {
                    animator.SetBool("IsWalking", false);
                }
                else
                {
                    bool isWalking = agent.velocity.sqrMagnitude > 0.15f && !agent.isStopped && !isWaiting;
                    animator.SetBool("IsWalking", isWalking);
                }
            }
        }
    }

    // LateUpdate runs after NavMeshAgent's PreLateUpdate internal reset.
    // Apply the rotation computed in Update() here so nothing overwrites it.
    // AiNavigation.LateUpdate() (same object, earlier component order) already
    // synced position, so we just need to commit rotation.
    void LateUpdate()
    {
        if (!_hasPendingRotation) return;
        transform.rotation = _pendingRotation;
        if (_rb != null) _rb.MoveRotation(_pendingRotation);
    }

    private IEnumerator WaitAndTurnRoutine()
    {
        isWaiting = true;

        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.isStopped = true;

        yield return new WaitForSeconds(idleDelay);

        if (navigation != null)
            navigation.GoToRandomWaypoint();

        yield return null;

        if (agent != null && agent.isActiveAndEnabled)
            agent.isStopped = false;

        isWaiting = false;
    }
}
