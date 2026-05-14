using UnityEngine;
using UnityEngine.AI;
using System.Collections; // Required for Coroutines

public class NavAgentConfig : MonoBehaviour
{
    public enum AgentRole { Worker, Forklift }
    public AgentRole role;

    private IEnumerator Start()
    {
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent == null) yield break;

        // 1. Wait until the end of the frame to ensure placement is stable
        yield return new WaitForEndOfFrame();

        // 2. Continuous check for NavMesh connectivity
        int retryCount = 0;
        while (!agent.isOnNavMesh && retryCount < 10)
        {
            retryCount++;
            yield return new WaitForSeconds(0.2f);
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"{gameObject.name} could not find NavMesh after retries.");
            yield break;
        }

        // 3. Apply Costs
        if (role == AgentRole.Worker)
        {
            agent.SetAreaCost(0, 20.0f); // Expensive regular floor
            agent.SetAreaCost(3, 50.0f); // Strongly avoid Forklift Lanes
            agent.SetAreaCost(4, 1.0f);  // Strongly prefer Pedestrian Lanes
        }
        else if (role == AgentRole.Forklift)
        {
            agent.SetAreaCost(0, 20.0f); // Expensive regular floor
            agent.SetAreaCost(3, 1.0f);  // Strongly prefer MHE Lanes
            agent.SetAreaCost(4, 50.0f); // Strongly avoid Pedestrian Lanes
            
            // Forklifts need a bit more room to breathe
            agent.stoppingDistance = 1.0f; 
        }

        // 4. Force a path recalculation so the agent notices the new costs immediately
        if (agent.hasPath)
        {
            Vector3 target = agent.destination;
            agent.ResetPath();
            agent.SetDestination(target);
        }

        Debug.Log($"{gameObject.name} Nav Costs initialized as {role}");
    }
}