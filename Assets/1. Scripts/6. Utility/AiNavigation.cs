using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Combined navigation script that handles agent configuration (costs/roles)
/// and waypoint-based movement logic.
/// </summary>
public class AiNavigation : MonoBehaviour
{
    public enum AgentRole { Worker, Forklift }
    public AgentRole role;

    private Transform[] waypoints;
    private NavMeshAgent agent;
    private int currentIndex = 0;
    private bool initialized = false;
    private VehicleThrottleAudio throttleAudio;
    private AmbientMumble mumbleAudio;
    private bool hasHonkedThisArrival = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        throttleAudio = GetComponent<VehicleThrottleAudio>();
        mumbleAudio = GetComponent<AmbientMumble>();
        SetupAgentType();
    }

    private void SetupAgentType()
    {
        if (agent == null) return;

        // Map AgentRole to the NavMesh Agent Type Name
        string targetTypeName = role == AgentRole.Worker ? "Humanoid" : "MHE";

        // Find the Agent Type ID by name
        int count = NavMesh.GetSettingsCount();
        for (int i = 0; i < count; i++)
        {
            var settings = NavMesh.GetSettingsByIndex(i);
            if (NavMesh.GetSettingsNameFromID(settings.agentTypeID) == targetTypeName)
            {
                agent.agentTypeID = settings.agentTypeID;
                break;
            }
        }
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
        if (agent == null) yield break;

        // 1. Wait for NavMesh connectivity and initialization
        // This is critical when loading from a save, as the NavMesh is baked AFTER spawning.
        int retryCount = 0;
        while (agent != null && !agent.isOnNavMesh && retryCount < 30)
        {
            retryCount++;
            
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                if (agent.isActiveAndEnabled && !agent.isOnNavMesh && (Mathf.Abs(hit.position.y - transform.position.y) < 2.0f || retryCount > 10))
                {
                    try { agent.Warp(hit.position); } catch { }
                }
            }
            
            yield return new WaitForSeconds(0.5f);
        }

        if (!agent.isOnNavMesh)
        {
            // We don't yield break here, maybe it will find it later in Update retry
        }

        // 2. Apply Costs (Merged from NavAgentConfig)
        ApplyAgentCosts();

        // 3. Robust Waypoint Finding
        // During load, waypoints might be instantiated after the agent.
        retryCount = 0;
        while ((waypoints == null || waypoints.Length == 0) && retryCount < 30)
        {
            FindWaypoints();
            if (waypoints.Length == 0)
            {
                retryCount++;
                yield return new WaitForSeconds(0.5f);
            }
        }

        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning($"[AiNavigation] {gameObject.name} could not find any waypoints after {retryCount} retries.");
        }
        else
        {
            // 4. Start Movement
            // Pick a random starting point
            currentIndex = Random.Range(0, waypoints.Length);
            
            // Initial destination set
            retryCount = 0;
            while (!initialized && retryCount < 5)
            {
                if (agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    if (agent.SetDestination(waypoints[currentIndex].position))
                    {
                        initialized = true;
                        break;
                    }
                }
                retryCount++;
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    private void ApplyAgentCosts()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        if (role == AgentRole.Worker)
        {
            agent.SetAreaCost(0, 25.0f); // Expensive regular floor
            agent.SetAreaCost(3, 80.0f); // Strongly avoid Forklift Lanes
            agent.SetAreaCost(4, 1.0f);  // Strongly prefer Pedestrian Lanes
        }
        else if (role == AgentRole.Forklift)
        {
            agent.SetAreaCost(0, 25.0f); // Expensive regular floor
            agent.SetAreaCost(3, 1.0f);  // Strongly prefer MHE Lanes
            agent.SetAreaCost(4, 80.0f); // Strongly avoid Pedestrian Lanes
            
            // Forklifts need a bit more room to breathe
            agent.stoppingDistance = 1.0f; 
        }

        // Force recalculation
        if (agent.hasPath)
        {
            Vector3 target = agent.destination;
            agent.ResetPath();
            agent.SetDestination(target);
        }
    }

    private void Update()
    {
        // Safety: if we failed to initialize, try again occasionally
        if (!initialized)
        {
            if (Time.frameCount % 60 == 0)
            {
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    if (waypoints != null && waypoints.Length > 0)
                    {
                        if (agent.SetDestination(waypoints[currentIndex].position))
                        {
                            initialized = true;
                        }
                    }
                }
            }
            return;
        }

        // Recovery: if we lost NavMesh (e.g. during a bake), wait and try to re-snap
        if (!agent.isOnNavMesh)
        {
            if (Time.frameCount % 30 == 0) // Check every half second-ish
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                {
                    // Only warp if it doesn't cause a massive vertical jump (which would be "not obeying height")
                    if (Mathf.Abs(hit.position.y - transform.position.y) < 1.0f)
                    {
                        agent.Warp(hit.position);
                        // Force destination refresh
                        if (waypoints != null && waypoints.Length > 0)
                            agent.SetDestination(waypoints[currentIndex].position);
                    }
                }
            }
            return;
        }

        // Arrival Honk & Mumble Logic
        if (throttleAudio != null || mumbleAudio != null)
        {
            bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;
            if (arrived && !hasHonkedThisArrival)
            {
                hasHonkedThisArrival = true;
                if (throttleAudio != null) throttleAudio.TriggerArrivalHonk();
                if (mumbleAudio != null) mumbleAudio.TryMumble();
            }
            else if (!arrived && agent.remainingDistance > agent.stoppingDistance + 0.5f)
{
                // Reset when we move significantly away from the point
                hasHonkedThisArrival = false;
            }
        }

        // Progression logic for agents without AgentAnimation
        if (GetComponent<AgentAnimation>() == null)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
            {
                GoToRandomWaypoint();
            }
        }
    }

    public void GoToRandomWaypoint()
    {
        // Re-find waypoints if the array is missing, empty, or contains destroyed references (common after quick-load)
        if (waypoints == null || waypoints.Length == 0 || (waypoints.Length > 0 && waypoints[0] == null))
        {
            FindWaypoints();
        }

        if (waypoints == null || waypoints.Length == 0) return;

        if (!agent.isOnNavMesh) return;

        int nextIndex = currentIndex;
        int safety = 0;
        // Only try to find a different index if we have more than one waypoint
        while (nextIndex == currentIndex && safety < 10 && waypoints.Length > 1)
        {
            nextIndex = Random.Range(0, waypoints.Length);
            safety++;
        }

        currentIndex = nextIndex;

        // Final safety check for the selected transform reference
        if (waypoints[currentIndex] == null)
        {
            FindWaypoints();
            if (waypoints == null || waypoints.Length == 0) return;
            currentIndex = Random.Range(0, waypoints.Length);
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh && waypoints[currentIndex] != null)
        {
            agent.SetDestination(waypoints[currentIndex].position);
        }
    }
}

