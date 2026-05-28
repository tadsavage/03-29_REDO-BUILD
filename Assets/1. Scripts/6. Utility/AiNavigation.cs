using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Handles agent role configuration and waypoint-based movement.
/// Startup is driven by NavMeshManager.OnNavMeshReady instead of blind polling,
/// so all agents begin moving the moment the bake finishes.
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
        string targetTypeName = role == AgentRole.Worker ? "Humanoid" : "MHE";
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

        // ── Wait for NavMesh ────────────────────────────────────────────────────
        // Subscribe to NavMeshManager's ready event instead of polling every 0.5s.
        // This means ALL agents snap to the mesh and start moving at the same frame.
        if (!NavMeshManager.IsReady)
        {
            bool navReady = false;
            System.Action onReady = () => navReady = true;
            NavMeshManager.OnNavMeshReady += onReady;

            float timeout = 20f; // hard safety cap
            while (!navReady && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            NavMeshManager.OnNavMeshReady -= onReady;

            if (timeout <= 0f)
                Debug.LogWarning($"[AiNavigation] {gameObject.name}: timed out waiting for NavMesh.");
        }

        // ── Snap to surface ─────────────────────────────────────────────────────
        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                try { agent.Warp(hit.position); } catch { }
            }
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"[AiNavigation] {gameObject.name}: still not on NavMesh after bake. Update() recovery will retry.");
            yield break;
        }

        // ── Apply lane costs ────────────────────────────────────────────────────
        ApplyAgentCosts();

        // ── Find waypoints ──────────────────────────────────────────────────────
        FindWaypoints();
        if (waypoints == null || waypoints.Length == 0)
        {
            yield return null; // give one frame for late-spawned waypoints
            FindWaypoints();
        }

        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning($"[AiNavigation] {gameObject.name}: no waypoints found.");
            yield break;
        }

        // ── Go ──────────────────────────────────────────────────────────────────
        currentIndex = Random.Range(0, waypoints.Length);
        if (agent.SetDestination(waypoints[currentIndex].position))
            initialized = true;
    }

    private void ApplyAgentCosts()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        if (role == AgentRole.Worker)
        {
            agent.SetAreaCost(0, 25.0f); // Walkable (generic) — expensive
            agent.SetAreaCost(3, 80.0f); // MHE lane   — strongly avoid
            agent.SetAreaCost(4, 1.0f);  // Ped lane   — strongly prefer
        }
        else if (role == AgentRole.Forklift)
        {
            agent.SetAreaCost(0, 25.0f); // Walkable (generic) — expensive
            agent.SetAreaCost(3, 1.0f);  // MHE lane   — strongly prefer
            agent.SetAreaCost(4, 80.0f); // Ped lane   — strongly avoid
            agent.stoppingDistance = 1.0f;
        }

        if (agent.hasPath)
        {
            Vector3 target = agent.destination;
            agent.ResetPath();
            agent.SetDestination(target);
        }
    }

    private void Update()
    {
        // ── Recovery: re-initialize if Start() gave up ──────────────────────────
        if (!initialized)
        {
            if (Time.frameCount % 60 == 0 && agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                if (waypoints == null || waypoints.Length == 0) FindWaypoints();
                if (waypoints != null && waypoints.Length > 0)
                {
                    ApplyAgentCosts();
                    if (agent.SetDestination(waypoints[currentIndex].position))
                        initialized = true;
                }
            }
            return;
        }

        // ── Recovery: re-snap if a runtime bake knocked us off the mesh ─────────
        if (!agent.isOnNavMesh)
        {
            if (Time.frameCount % 30 == 0)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                {
                    if (Mathf.Abs(hit.position.y - transform.position.y) < 1.0f)
                    {
                        agent.Warp(hit.position);
                        if (waypoints != null && waypoints.Length > 0)
                            agent.SetDestination(waypoints[currentIndex].position);
                    }
                }
            }
            return;
        }

        // ── Arrival audio ────────────────────────────────────────────────────────
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
                hasHonkedThisArrival = false;
            }
        }

        // ── Waypoint progression (for agents without AgentAnimation) ─────────────
        if (GetComponent<AgentAnimation>() == null)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
                GoToRandomWaypoint();
        }
    }

    public void GoToRandomWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0 || (waypoints.Length > 0 && waypoints[0] == null))
            FindWaypoints();

        if (waypoints == null || waypoints.Length == 0) return;
        if (!agent.isOnNavMesh) return;

        int nextIndex = currentIndex;
        int safety = 0;
        while (nextIndex == currentIndex && safety < 10 && waypoints.Length > 1)
        {
            nextIndex = Random.Range(0, waypoints.Length);
            safety++;
        }
        currentIndex = nextIndex;

        if (waypoints[currentIndex] == null)
        {
            FindWaypoints();
            if (waypoints == null || waypoints.Length == 0) return;
            currentIndex = Random.Range(0, waypoints.Length);
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh && waypoints[currentIndex] != null)
            agent.SetDestination(waypoints[currentIndex].position);
    }
}
