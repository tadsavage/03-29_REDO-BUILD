using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class AiNavigation : MonoBehaviour
{
    private Transform[] waypoints;
    private NavMeshAgent agent;
    private int currentIndex = 0;
    private bool initialized = false;

    private void Awake()
    {
        // Gather all Waypoint components in the scene
        FindWaypoints();
    }

    private void FindWaypoints()
    {
        Waypoint[] found = Object.FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        waypoints = new Transform[found.Length];

        for (int i = 0; i < found.Length; i++)
            waypoints[i] = found[i].transform;
    }

    private IEnumerator Start()
    {
        agent = GetComponent<NavMeshAgent>();
        
        // Wait a few frames to ensure the object is properly placed and warped in the world.
        yield return new WaitForSeconds(0.1f);

        if (agent == null) yield break;

        // Ensure agent is active
        agent.enabled = true;

        // Try to snap to NavMesh if not already on it
        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

        // Delay starting movement so user can see where they placed the object
        // and to prevent the 'sliding out' feeling immediately after placement.
        yield return new WaitForSeconds(1.0f);

        if (waypoints == null || waypoints.Length == 0)
        {
            FindWaypoints();
        }

        if (waypoints.Length > 0)
        {
            // Pick a random starting point
            currentIndex = Random.Range(0, waypoints.Length);
            
            // Retry loop for initial destination
            int retries = 5; // Reduced retries since placement is more robust now
            while (retries > 0)
            {
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    // Basic sanity check: is the waypoint somewhat reachable?
                    Vector3 targetPos = waypoints[currentIndex].position;
                    if (agent.SetDestination(targetPos))
                    {
                        initialized = true;
                        break;
                    }
                }
                
                retries--;
                yield return new WaitForSeconds(0.2f);
            }
        }
}

    void Update()
    {
        // Safety: if we failed to initialize, try again occasionally
        if (!initialized && Time.frameCount % 60 == 0)
        {
            if (waypoints.Length > 0 && agent != null && agent.isOnNavMesh)
            {
                if (agent.SetDestination(waypoints[currentIndex].position))
                {
                    initialized = true;
                }
            }
        }
    }

    public void GoToRandomWaypoint()
    {
        if (waypoints == null || waypoints.Length <= 1) return;

        int nextIndex = currentIndex;
        int safety = 0;
        while (nextIndex == currentIndex && safety < 10)
        {
            nextIndex = Random.Range(0, waypoints.Length);
            safety++;
        }

        currentIndex = nextIndex;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(waypoints[currentIndex].position);
        }
    }
}

