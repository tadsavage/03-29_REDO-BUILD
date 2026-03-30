using UnityEngine;
using UnityEngine.AI;

public class AiNavigation : MonoBehaviour
{
    private Transform[] waypoints;
    [SerializeField] private float waypointThreshold = 0.5f;

    private NavMeshAgent agent;
    private int currentIndex = 0;

    private void Awake()
    {
        // Automatically gather all Waypoint components in the scene
        Waypoint[] found = Object.FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        waypoints = new Transform[found.Length];

        for (int i = 0; i < found.Length; i++)
            waypoints[i] = found[i].transform;

    }
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();


        if (waypoints.Length > 0)
        {
            // Pick a random starting point
            currentIndex = Random.Range(0, waypoints.Length);
            agent.SetDestination(waypoints[currentIndex].position);
        }
    }

    void Update()
    {
        if (agent.pathPending) return;

        if (agent.remainingDistance <= waypointThreshold)
        {
            GoToRandomWaypoint();
        }
    }

    void GoToRandomWaypoint()
    {
        if (waypoints.Length <= 1) return;

        // Keep picking a new index until it's different from the current one
        int nextIndex = currentIndex;
        while (nextIndex == currentIndex)
        {
            nextIndex = Random.Range(0, waypoints.Length);
        }

        currentIndex = nextIndex;
        agent.SetDestination(waypoints[currentIndex].position);
    }
}

