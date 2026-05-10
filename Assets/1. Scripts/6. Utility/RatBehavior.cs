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

    private enum RatState { Idle, Circling, ScurryingOff, Sniffing }
    private RatState currentState;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        agent.speed = scurrySpeed;
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
            if (Random.value > 0.5f)
            {
                yield return StartCoroutine(SniffRoutine());
            }
        }
    }

    private IEnumerator ScurryInCircles()
    {
        currentState = RatState.Circling;
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

                agent.SetDestination(target);
                while (agent.pathPending || agent.remainingDistance > 0.2f) yield return null;
            }
        }
    }

    private IEnumerator ScurryOff()
    {
        currentState = RatState.ScurryingOff;
        // Find a random point within 10 meters
        Vector3 randomDirection = Random.insideUnitSphere * 10f;
        randomDirection += transform.position;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, 10f, 1))
        {
            agent.SetDestination(hit.position);
            while (agent.pathPending || agent.remainingDistance > 0.5f) yield return null;
        }
        yield return new WaitForSeconds(Random.Range(1f, 2f));
    }

    private IEnumerator SniffRoutine()
    {
        currentState = RatState.Sniffing;
        agent.isStopped = true;
        animator.SetTrigger("Sniff");

        // Wait for the animation to play (approx 2-3 seconds)
        yield return new WaitForSeconds(2.5f);

        agent.isStopped = false;
    }

    void Update()
    {
        // Keep the animator in sync with movement
        bool isMoving = agent.velocity.magnitude > 0.1f && !agent.isStopped;
        animator.SetBool("IsWalking", isMoving);
    }
}