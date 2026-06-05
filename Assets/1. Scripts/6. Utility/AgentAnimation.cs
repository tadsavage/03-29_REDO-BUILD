using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class AgentAnimation : MonoBehaviour
{
    private NavMeshAgent       agent;
    private Animator           animator;
    private AiNavigation       navigation;
    private NoWaypointIndicator _indicator;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed        = 2f;
    [SerializeField] private float turnSpeed        = 200f;  // degrees per second
    [SerializeField] private float idleDelay        = 1.5f;  // pause at each waypoint
    [SerializeField] private float waypointThreshold = 0.5f;

    [Header("Blocked / Wave")]
    [Tooltip("Seconds after the ! indicator appears before the waving animation starts.")]
    [SerializeField] private float waveDelay = 2.5f;

    private bool  isWaiting;
    private bool  _everHadPath;   // latches true once agent has navigated at least once
    private float _waveDelayTimer;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        agent      = GetComponent<NavMeshAgent>();
        animator   = GetComponent<Animator>();
        navigation = GetComponent<AiNavigation>();
        _indicator = GetComponent<NoWaypointIndicator>();

        agent.speed           = walkSpeed;
        agent.angularSpeed    = turnSpeed;
        agent.acceleration    = 12f;
        agent.stoppingDistance = waypointThreshold;

        // Manual rotation — AiNavigation owns stair traversal rotation separately.
        agent.updateRotation = false;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!agent.isOnNavMesh)
        {
            if (animator != null) animator.SetBool("IsWalking", false);
            return;
        }

        // Latch: once we've ever had a path or velocity we've been navigating.
        if (agent.hasPath || agent.velocity.sqrMagnitude > 0.01f) _everHadPath = true;

        // ── Arrival detection ─────────────────────────────────────────────────
        // Only advance to the next waypoint on a FULLY COMPLETE path arrival.
        // PathPartial agents reach the nearest reachable point — that is NOT a
        // genuine arrival; the "!" indicator handles that state instead.
        bool isAtDestination = !agent.pathPending
            && agent.remainingDistance <= agent.stoppingDistance + 0.1f
            && agent.path.status == NavMeshPathStatus.PathComplete;

        if (isAtDestination && !isWaiting && _everHadPath)
            StartCoroutine(WaitAndTurnRoutine());

        // ── Manual rotation ────────────────────────────────────────────────────
        if (!isWaiting && agent.desiredVelocity.sqrMagnitude > 0.01f)
        {
            // Moving — face the direction of travel.
            Quaternion targetRot = Quaternion.LookRotation(agent.desiredVelocity.normalized);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }
        else if (!isWaiting && !isAtDestination && agent.hasPath && !agent.pathPending)
        {
            // Physically blocked — turn toward the next path corner so the agent
            // faces the obstacle rather than staring into empty space.
            Vector3 dir = agent.steeringTarget - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                Quaternion destRot = Quaternion.LookRotation(dir.normalized);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, destRot, turnSpeed * Time.deltaTime);
            }
        }

        // ── Animation sync ─────────────────────────────────────────────────────
        if (animator != null)
        {
            // Wave when the indicator is showing (either "?" or "!").
            // The 2.5 s delay means the agent stands idle for a beat before waving.
            bool indicatorVisible = _indicator != null && _indicator.IsShowingIndicator;

            if (indicatorVisible)
                _waveDelayTimer += Time.deltaTime;
            else
                _waveDelayTimer = 0f;

            bool isWaving = indicatorVisible && _waveDelayTimer >= waveDelay;
            animator.SetBool("IsWaving", isWaving);

            if (isWaving)
            {
                // Waving takes full priority — stop the walk animation.
                animator.SetBool("IsWalking", false);
            }
            else
            {
                float moveHeadingDot = 0f;
                if (agent.velocity.sqrMagnitude > 0.001f)
                    moveHeadingDot = Vector3.Dot(transform.forward, agent.velocity.normalized);

                bool isWalking = agent.velocity.sqrMagnitude > 0.15f
                              && !agent.isStopped
                              && moveHeadingDot > 0.5f;
                animator.SetBool("IsWalking", isWalking);
            }
        }
    }

    // ── Waypoint arrival ──────────────────────────────────────────────────────

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
