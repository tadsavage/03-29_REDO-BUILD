using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class AgentAnimation : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;
    private AiNavigation navigation;

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

    void Start()
    {
        agent      = GetComponent<NavMeshAgent>();
        animator   = GetComponent<Animator>();
        navigation = GetComponent<AiNavigation>();
        _indicator = GetComponent<NoWaypointIndicator>();

        // Setup agent
        agent.speed = walkSpeed;
        agent.angularSpeed = turnSpeed;
        agent.acceleration = 12f;
        agent.stoppingDistance = waypointThreshold;

        // CRITICAL: Disable auto-rotation to ensure we have full control over the heading.
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
        // PathComplete guard: a PathPartial agent reaches remainingDistance≈0 at the
        // nearest reachable point (edge of the blockage) — that is NOT a real arrival.
        // Without this check WaitAndTurnRoutine fires and the agent loops endlessly.
        bool isAtDestination = !agent.pathPending
            && agent.remainingDistance <= agent.stoppingDistance + 0.1f
            && agent.pathStatus == NavMeshPathStatus.PathComplete;

        // Only advance to the next waypoint on genuine arrival.
        // When blocked by an obstacle the agent stays on its current destination —
        // NoWaypointIndicator shows the "?" and the player resolves the blockage.
        if (isAtDestination && !isWaiting && _everHadPath)
        {
            StartCoroutine(WaitAndTurnRoutine());
        }

        // 2. Manual Rotation
        if (!isWaiting && agent.desiredVelocity.sqrMagnitude > 0.01f)
        {
            // Moving — face the direction of travel.
            Quaternion targetRot = Quaternion.LookRotation(agent.desiredVelocity.normalized);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                turnSpeed * Time.deltaTime
            );
        }
        else if (!isWaiting && !isAtDestination && agent.hasPath && !agent.pathPending)
        {
            // Physically blocked — face the next path corner so the agent looks
            // toward the obstacle rather than staring into space.
            Vector3 dir = agent.steeringTarget - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                Quaternion destRot = Quaternion.LookRotation(dir.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, destRot, turnSpeed * Time.deltaTime);
            }
        }

        // 3. Animation Sync
        if (animator != null)
        {
            // Ledge traversal takes highest priority
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
                    float moveHeadingDot = 0f;
                    if (agent.velocity.sqrMagnitude > 0.001f)
                        moveHeadingDot = Vector3.Dot(transform.forward, agent.velocity.normalized);

                    bool isWalking = agent.velocity.sqrMagnitude > 0.15f && !agent.isStopped && moveHeadingDot > 0.5f;
                    animator.SetBool("IsWalking", isWalking);
                }
            }
        }
    }

    private IEnumerator WaitAndTurnRoutine()
    {
        isWaiting = true;

        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.isStopped = true;

        yield return new WaitForSeconds(idleDelay);

        // Pick the next waypoint — always re-scans so newly placed ones are found.
        if (navigation != null)
            navigation.GoToRandomWaypoint();

        // One frame for the path request to register before resuming.
        yield return null;

        // Always resume — cleanup runs regardless of NavMesh state so the agent
        // is never left permanently stopped.
        if (agent != null && agent.isActiveAndEnabled)
            agent.isStopped = false;

        isWaiting = false;
    }
}

