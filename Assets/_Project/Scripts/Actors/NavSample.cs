using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NavMesh.SamplePosition(pos, out hit, dist, int areaMask) always queries agent type 0 ("Rat", radius 0.1), whatever the
/// caller's agent is. A drive target snapped that way lands on mesh the real agent (MHE radius 0.5, Human 0.25) cannot stand on or
/// path to: floor edges, narrow aisles, stranded slivers. The agent then logged "cannot reach task position (path invalid)" and
/// handed the task back (blocks 34-36: the same few coordinates, x -44..-47 and x 3..11 / z 44..51, over and over).
/// Always sample with the agent's own type.
/// </summary>
public static class NavSample
{
    /// <summary>SamplePosition on <paramref name="agent"/>'s own agent type and area mask.</summary>
    public static bool OnAgentMesh(NavMeshAgent agent, Vector3 position, float maxDistance, out NavMeshHit hit)
    {
        if (agent == null) return NavMesh.SamplePosition(position, out hit, maxDistance, NavMesh.AllAreas);
        return OnAgentMesh(agent.agentTypeID, agent.areaMask, position, maxDistance, out hit);
    }

    public static bool OnAgentMesh(int agentTypeId, int areaMask, Vector3 position, float maxDistance, out NavMeshHit hit)
    {
        var filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = areaMask };
        return NavMesh.SamplePosition(position, out hit, maxDistance, filter);
    }
}
