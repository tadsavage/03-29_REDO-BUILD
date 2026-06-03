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
    // Latches true once the agent has ever had a path, preventing spurious
    // WaitAndTurnRoutine triggers before the first destination is assigned.
    private bool _everHadPath;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        navigation = GetComponent<AiNavigation>();

        // Setup agent
        agent.speed = walkSpeed;
        agent.angularSpeed = turnSpeed;
        agent.acceleration = 12f;
        agent.stoppingDistance = waypointThreshold;

        // CRITICAL: Disable auto-rotation to ensure we have full control over the heading.
        agent.updateRotation = false;
    }

    private float stuckTimer = 0f;
    private const float STUCK_TIMEOUT = 5f;

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
        bool isAtDestination = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;

        if (isAtDestination && !isWaiting && _everHadPath)
        {
            StartCoroutine(WaitAndTurnRoutine());
        }
        else if (agent.hasPath && agent.velocity.sqrMagnitude < 0.01f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > STUCK_TIMEOUT)
            {
                stuckTimer = 0;
                if (navigation != null) navigation.GoToRandomWaypoint();
            }
        }
        else
        {
            stuckTimer = 0;
        }

        // 2. Manual Rotation: Always face intended movement direction
        if (!isWaiting && agent.desiredVelocity.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(agent.desiredVelocity.normalized);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                turnSpeed * Time.deltaTime
            );
        }

        // 3. Animation Sync
        if (animator != null)
        {
            // Only walk if moving forward relative to our heading
            float moveHeadingDot = 0f;
            if (agent.velocity.sqrMagnitude > 0.001f)
                moveHeadingDot = Vector3.Dot(transform.forward, agent.velocity.normalized);

            bool isWalking = agent.velocity.sqrMagnitude > 0.15f && !agent.isStopped && moveHeadingDot > 0.5f;
            animator.SetBool("IsWalking", isWalking);
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

