using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class RatBehavior : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;

    [Header("Movement Settings")]
    [SerializeField] private float circleDiameter = 3.5f;
    [SerializeField] private int circlePoints = 8;
    [SerializeField] private float scurrySpeed = 5f;
    [SerializeField] private float angularSpeed = 720f;
    [SerializeField] private float acceleration = 20f;

    //private enum RatState { Idle, Circling, ScurryingOff, Sniffing }
    //private RatState currentState;

    private IEnumerator Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        agent.speed = scurrySpeed;
        agent.acceleration = acceleration;
        agent.updateRotation = false;

        // WAIT A FRAME to allow the agent to snap to the NavMesh
        yield return null;

        // Check if we are on the NavMesh; if not, try to warp to the nearest valid point
        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

        StartCoroutine(BehaviorRoutine());
    }

    private IEnumerator BehaviorRoutine()
    {
        while (true)
        {
            // 1. Scurry in circles
            yield return StartCoroutine(ScurryInCircles());

            // 2. Scurry off somewhere else
            yield return StartCoroutine(ScurryOff());

            // 3. Occasionally pause and sniff
            if (Random.value > 0.1f)
            {
                yield return StartCoroutine(SniffRoutine());
            }
        }
    }

    private IEnumerator WaitForPath(float stoppingDist)
    {
        // Give it a frame to start calculating
        yield return null;

        while (agent.isActiveAndEnabled)
        {
            // If we are off-navmesh, wait until we find it again
            if (!agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            // Standard arrival check
            if (!agent.pathPending && agent.remainingDistance <= stoppingDist)
            {
                break;
            }

            yield return null;
        }
    }

    private IEnumerator ScurryInCircles()
    {
        //currentState = RatState.Circling;
        Vector3 center = transform.position;
        float radius = circleDiameter / 2f;

        // Do 1-2 full rotations
        int rotations = Random.Range(1, 3);
        for (int r = 0; r < rotations; r++)
        {
            for (int i = 0; i < circlePoints; i++)
            {
                float angle = i * Mathf.PI * 2 / circlePoints;
                Vector3 target = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);

                if (agent.isOnNavMesh)
                {
                    agent.SetDestination(target);
                    yield return StartCoroutine(WaitForPath(0.2f));
                }
                else
                {
                    yield return new WaitForSeconds(0.5f);
                }
            }
        }
    }

    private IEnumerator ScurryOff()
    {
        //currentState = RatState.ScurryingOff;
        // Find a random point within 10 meters
        Vector3 randomDirection = Random.insideUnitSphere * 10f;
        randomDirection += transform.position;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, 10f, 1))
        {
            if (agent.isOnNavMesh)
            {
                agent.SetDestination(hit.position);
                yield return StartCoroutine(WaitForPath(0.5f));
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
            }
        }
        yield return new WaitForSeconds(Random.Range(.10f, .5f));
    }

    private IEnumerator SniffRoutine()
    {
        //currentState = RatState.Sniffing;
        agent.isStopped = true;
        animator.SetTrigger("Sniff");

        // Wait for the animation to play (approx 2-3 seconds)
        yield return new WaitForSeconds(4f);

        agent.isStopped = false;
    }

    void Update()
    {
        // Keep the animator in sync with movement
        bool isMoving = agent.velocity.magnitude > 0.1f && !agent.isStopped;
        animator.SetBool("IsWalking", isMoving);

        // Manually rotate to face movement direction with a 180-degree offset
        if (isMoving && agent.velocity.sqrMagnitude > 0.01f)
        {
            Vector3 moveDirection = agent.velocity.normalized;
            //moveDirection.y = 0; // Keep the rat level

            if (moveDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                // Apply the 180-degree flip
                Quaternion correctedRotation = targetRotation * Quaternion.Euler(0, 180, 0);
                
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, 
                    correctedRotation, 
                    Time.deltaTime * (angularSpeed / 10f) // Adjusted for better responsiveness
                );
            }
        }
    }
}