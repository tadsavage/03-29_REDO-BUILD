using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

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

    [Header("Exterminator & Hiding")]
    [Tooltip("Distance to exterminator that triggers scurrying.")]
    [SerializeField] private float detectionRange = 4.0f;
    [SerializeField] private string exterminatorName = "Exterminator";
    [SerializeField] private float hidingChance = 0.6f; // Increased default
    [SerializeField] private float panicDuration = 8.0f;
    [SerializeField] private string[] palletNames = { "A Chep", "StackPlts", "Cases" };
    [SerializeField] private string palletCategory = "Inventory";

    private bool isHiding = false;
    private bool isScurryingAway = false;
    private float lastDetectionTime;
    private bool exterminatorNearCached = false;
    private Renderer[] visuals;

    private IEnumerator Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        visuals = GetComponentsInChildren<Renderer>();

        agent.speed = scurrySpeed;
        agent.acceleration = acceleration;
        agent.updateRotation = false;

        // ... existing start logic ...
        yield return null;

        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                if (Mathf.Abs(hit.position.y - transform.position.y) < 1.0f)
                {
                    agent.Warp(hit.position);
                }
            }
        }

        StartCoroutine(BehaviorRoutine());
    }

    private IEnumerator BehaviorRoutine()
    {
        while (true)
        {
            if (isHiding)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            // 1. Occasionally try to find a pallet to hide in
            if (Random.value < hidingChance)
            {
                yield return StartCoroutine(GoToHidingSpot());
                if (isHiding) continue;
            }

            // 2. Scurry in circles
            yield return StartCoroutine(ScurryInCircles());

            // 3. Scurry off somewhere else
            yield return StartCoroutine(ScurryOff());

            // 4. Occasionally pause and sniff
            if (Random.value > 0.1f)
            {
                yield return StartCoroutine(SniffRoutine());
            }
        }
    }

    private void SetVisuals(bool visible)
    {
        if (visuals == null) return;
        foreach (var r in visuals) r.enabled = visible;
    }

    private IEnumerator GoToHidingSpot()
    {
        Transform spot = FindNearestHidingSpot();
        if (spot != null)
        {
            agent.SetDestination(spot.position);
            yield return StartCoroutine(WaitForPath(0.1f));
            
            if (!agent.pathPending && agent.remainingDistance < 0.5f)
            {
                isHiding = true;
                agent.isStopped = true;
                agent.enabled = false; // Disable agent so it doesn't push others or block
                SetVisuals(false);     // DISAPPEAR
            }
        }
    }

    private Transform FindNearestHidingSpot()
    {
        Transform nearest = null;
        float minDist = 15f; 
        foreach (var obj in PlacedObjectRegistry.All)
        {
            if (obj == null || obj.data == null) continue;

            bool isPallet = false;
            if (obj.data.category == palletCategory) isPallet = true;
            else
            {
                foreach (string pName in palletNames)
                {
                    if (obj.data.objName.Contains(pName)) { isPallet = true; break; }
                }
            }

            if (isPallet)
            {
                float d = Vector3.Distance(transform.position, obj.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = obj.transform;
                }
            }
        }
        return nearest;
    }

    private IEnumerator ScurryAwayRoutine()
    {
        isScurryingAway = true;
        isHiding = false;
        
        // REAPPEAR
        SetVisuals(true);
        agent.enabled = true;
        yield return null; // Wait for agent to enable
        
        agent.isStopped = false;
        agent.speed = scurrySpeed * 1.5f; // Extra speed when panicking

        float panicEndTime = Time.time + panicDuration;
        
        while (Time.time < panicEndTime)
        {
            Vector3 randomDirection = Random.insideUnitSphere * 12f;
            randomDirection.y = 0;
            Vector3 target = transform.position + randomDirection;

            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3.0f, agent.areaMask))
            {
                agent.SetDestination(hit.position);
                
                // Wait until we reach the point or panic duration ends
                float pointTimeout = Time.time + 3.0f;
                while (Time.time < pointTimeout && Time.time < panicEndTime)
                {
                    if (!agent.pathPending && agent.remainingDistance <= 0.5f)
                        break;
                    yield return null;
                }
            }
            yield return null;
        }

        agent.speed = scurrySpeed;
        isScurryingAway = false;
        StartCoroutine(BehaviorRoutine());
    }

    private bool IsExterminatorNear()
    {
        if (Time.time - lastDetectionTime < 0.2f) return exterminatorNearCached;
        
        lastDetectionTime = Time.time;
        exterminatorNearCached = false;

        foreach (var obj in PlacedObjectRegistry.All)
        {
            if (obj != null && obj.data != null && obj.data.objName == exterminatorName)
            {
                if (Vector3.Distance(transform.position, obj.transform.position) < detectionRange)
                {
                    exterminatorNearCached = true;
                    return true;
                }
            }
        }
        return false;
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
                    if (Mathf.Abs(hit.position.y - transform.position.y) < 1.0f)
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
        // Find a random point within 10 meters
        Vector3 randomDirection = Random.insideUnitSphere * 10f;
        Vector3 target = transform.position + randomDirection;

        // Use a 2.0f radius and agent.areaMask to ensure we stay on the same floor level
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2.0f, agent.areaMask))
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
        agent.isStopped = true;
        animator.SetTrigger("Sniff");

        // Wait for the animation to play
        yield return new WaitForSeconds(4f);

        agent.isStopped = false;
    }

    void Update()
    {
        // Immediate reaction to exterminator (works even when hiding)
        if (!isScurryingAway && IsExterminatorNear())
        {
            StopAllCoroutines();
            StartCoroutine(ScurryAwayRoutine());
            return;
        }

        if (!agent.isActiveAndEnabled)
        {
            animator.SetBool("IsWalking", false);
            return;
        }

        // Keep the animator in sync with movement
        bool isMoving = agent.velocity.magnitude > 0.1f && !agent.isStopped;
        animator.SetBool("IsWalking", isMoving);

        // Manually rotate to face movement direction with a 180-degree offset
        if (isMoving && agent.velocity.sqrMagnitude > 0.01f)
        {
            Vector3 moveDirection = agent.velocity.normalized;

            if (moveDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                // Apply the 180-degree flip
                Quaternion correctedRotation = targetRotation * Quaternion.Euler(0, 180, 0);
                
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, 
                    correctedRotation, 
                    Time.deltaTime * (angularSpeed / 10f)
                );
            }
        }
    }
}