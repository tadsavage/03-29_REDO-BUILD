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

    [Header("Animation Settings")]
    [SerializeField] private float animationTurnThreshold = 100f; // Lower threshold for responsive turn animations

    private Vector3 lastForward;
    private bool isWaiting;
    private float smoothedTurnRate;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        navigation = GetComponent<AiNavigation>();

        lastForward = transform.forward;
        
        // Setup agent
        agent.speed = walkSpeed;
        agent.angularSpeed = turnSpeed;
        agent.acceleration = 12f;
        agent.stoppingDistance = waypointThreshold;
    }

    private float stuckTimer = 0f;
    private const float STUCK_TIMEOUT = 5f;

    void Update()
    {
        // Safety: if we lost NavMesh (e.g. during a bake), wait
        if (!agent.isOnNavMesh)
        {
            if (animator != null)
            {
                animator.SetBool("IsWalking", false);
                animator.SetBool("IsTurningLeft", false);
                animator.SetBool("IsTurningRight", false);
            }
            return;
        }

        // 1. Flow Control
        bool isAtDestination = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;
        
        if (isAtDestination)
        {
            if (!isWaiting && (agent.velocity.sqrMagnitude > 0.1f || agent.hasPath))
            {
                StartCoroutine(WaitAndTurnRoutine());
            }
        }
        else if (agent.hasPath && agent.velocity.sqrMagnitude < 0.01f)
        {
            // Stuck detection: if we have a path but aren't moving
            stuckTimer += Time.deltaTime;
            if (stuckTimer > STUCK_TIMEOUT)
            {
                stuckTimer = 0;
                Debug.Log($"{gameObject.name} detected as stuck. Re-routing...");
                if (navigation != null) navigation.GoToRandomWaypoint();
            }
        }
        else
        {
            stuckTimer = 0;
        }

        // 2. Animation Sync
        UpdateAnimations();
    }

    private IEnumerator WaitAndTurnRoutine()
    {
        isWaiting = true;
        
        // Stop movement
        agent.isStopped = true;
        
        // Decelerate velocity manually for smoothness
        float decelTime = 0.2f;
        while (decelTime > 0)
        {
            agent.velocity = Vector3.Lerp(agent.velocity, Vector3.zero, Time.deltaTime * 10f);
            decelTime -= Time.deltaTime;
            yield return null;
        }
        agent.velocity = Vector3.zero;

        // Pause at waypoint
        yield return new WaitForSeconds(idleDelay);

        // Get new destination
        if (navigation != null)
        {
            navigation.GoToRandomWaypoint();
        }

        // Wait for path
        float timeout = 1.0f;
        while (agent.pathPending && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        // Turn to target
        if (agent.hasPath)
        {
            Vector3 targetDir = (agent.steeringTarget - transform.position);
            targetDir.y = 0;

            if (targetDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(targetDir.normalized);
                
                // Rotate smoothly
                while (Quaternion.Angle(transform.rotation, targetRot) > 2f)
                {
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation, 
                        targetRot, 
                        turnSpeed * Time.deltaTime
                    );
                    yield return null;
                }
                transform.rotation = targetRot;
            }
        }

        // Resume
        agent.isStopped = false;
        isWaiting = false;
    }

    private void UpdateAnimations()
    {
        Vector3 currentForward = transform.forward;

        // Turn rate calculation
        float angleDiff = Vector3.SignedAngle(lastForward, currentForward, Vector3.up);
        float rawTurnRate = angleDiff / Time.deltaTime;
        
        // Smooth turn rate to avoid flickering
        smoothedTurnRate = Mathf.Lerp(smoothedTurnRate, rawTurnRate, Time.deltaTime * 8f);

        bool isTurningLeft = rawTurnRate < -animationTurnThreshold;
        bool isTurningRight = rawTurnRate > animationTurnThreshold;
        bool isWalking = agent.velocity.sqrMagnitude > 0.15f && !agent.isStopped;

        // Update Animator
        if (animator != null)
        {
            animator.SetBool("IsTurningLeft", isTurningLeft);
            animator.SetBool("IsTurningRight", isTurningRight);
            animator.SetBool("IsWalking", isWalking);
        }

        lastForward = currentForward;
    }
}
