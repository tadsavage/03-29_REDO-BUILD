using UnityEngine;

/// <summary>
/// Placed on every agent prefab root so doors (and any other interactive objects)
/// can identify the agent's type without coupling to AiNavigation or RatBehavior.
/// </summary>
public class AgentTypeTag : MonoBehaviour
{
    public AgentType agentType = AgentType.Human;
}
