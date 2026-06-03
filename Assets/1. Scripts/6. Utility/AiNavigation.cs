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

    [Header("Footsteps")]
    [SerializeField] private AudioClip footstepClip;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.6f;

    private Transform[] waypoints;
    private NavMeshAgent agent;
    private int currentIndex = 0;
    private bool initialized = false;
    // How long the agent has been without a valid path — used to debounce
    // the '?' so it doesn't flash during the brief gap between waypoints.
    private float _noPathTimer = 0f;
    private const float NoPathShowDelay = 0.5f;
    private VehicleThrottleAudio throttleAudio;
    private bool hasHonkedThisArrival = false;
    private bool _traversingLink = false;
    private AgentAnimation _agentAnimation;
    private AudioSource _footstepSource;
    private NoWaypointIndicator _indicator;

    private void OnEnable()
    {
        NavMeshManager.OnNavMeshReady += OnNavMeshBaked;
    }

    private void OnDisable()
    {
        NavMeshManager.OnNavMeshReady -= OnNavMeshBaked;
    }

    // Called every time the NavMesh finishes a bake (e.g. a waypoint or tile was placed).
    // Refreshes the waypoint list and drives the indicator; if the agent is idle it kicks
    // off navigation immediately rather than waiting for the 60-frame polling cycle.
    private void OnNavMeshBaked()
    {
        if (agent == null || !agent.isActiveAndEnabled) return;

        FindWaypoints();

        if (!agent.isOnNavMesh) return;

        if (!initialized && waypoints != null && waypoints.Length > 0)
        {
            ApplyAgentCosts();
            currentIndex = Random.Range(0, waypoints.Length);
            if (agent.SetDestination(waypoints[currentIndex].position))
                initialized = true;
        }
        else if (initialized && !agent.hasPath && !agent.pathPending
                 && waypoints != null && waypoints.Length > 0)
        {
            GoToRandomWaypoint();
        }
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        throttleAudio = GetComponent<VehicleThrottleAudio>();
        _agentAnimation = GetComponent<AgentAnimation>();
        _indicator = GetComponent<NoWaypointIndicator>();
        SetupAgentType();

        if (footstepClip != null)
        {
            _footstepSource = gameObject.AddComponent<AudioSource>();
            _footstepSource.clip = footstepClip;
            _footstepSource.loop = true;
            _footstepSource.volume = footstepVolume;
            _footstepSource.spatialBlend = 1f;
            _footstepSource.rolloffMode = AudioRolloffMode.Logarithmic;
            _footstepSource.minDistance = 2f;
            _footstepSource.maxDistance = 20f;
            _footstepSource.playOnAwake = false;
        }

        // Auto-register agent type for door access — won't override a manually set tag
        if (GetComponent<AgentTypeTag>() == null)
        {
            var tag = gameObject.AddComponent<AgentTypeTag>();
            tag.agentType = role == AgentRole.Worker ? AgentType.Human : AgentType.MHE;
        }
    }

    private void SetupAgentType()
    {
        if (agent == null) return;
        string targetTypeName = role == AgentRole.Worker ? "Human" : "MHE";
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
        Waypoint.WaypointGroup myGroup = RoleToWaypointGroup();

        Waypoint[] all = Object.FindObjectsByType<Waypoint>(FindObjectsSortMode.None);

        var matching = new System.Collections.Generic.List<Transform>();
        foreach (var wp in all)
        {
            // Rats roam everywhere — handled in RatBehavior, not here.
            // All other agents only visit waypoints that include their group.
            if (wp.AllowsGroup(myGroup))
                matching.Add(wp.transform);
        }

        waypoints = matching.ToArray();

        if (waypoints.Length == 0)
        {
            _indicator?.Show();
            Debug.LogWarning($"[AiNavigation] {gameObject.name}: no waypoints for group '{myGroup}'.");
        }
        else
        {
            // Waypoints just became available — fade the '?' out instead of snapping it off.
            _indicator?.FadeOut();
        }
    }

    private Waypoint.WaypointGroup RoleToWaypointGroup()
    {
        // Map AiNavigation role → WaypointGroup flag
        // Security and Boss use their own roles; Workers and Forklifts map directly.
        // Check the AgentTypeTag for more specific types.
        var tag = GetComponent<AgentTypeTag>();
        if (tag != null)
        {
            if (tag.agentType == AgentType.MHE) return Waypoint.WaypointGroup.MHE;
        }

        // Check object name for specific staff types until dedicated AiNavigation roles exist
        string n = gameObject.name.ToLower();
        if (n.Contains("boss"))         return Waypoint.WaypointGroup.Boss;
        if (n.Contains("security"))     return Waypoint.WaypointGroup.Security;
        if (n.Contains("exterminator")) return Waypoint.WaypointGroup.Exterminator;

        return role == AgentRole.Forklift
            ? Waypoint.WaypointGroup.MHE
            : Waypoint.WaypointGroup.Worker;
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
        // Use a query filter so we only snap to the mesh that this agent type
        // can actually navigate (Human vs MHE surfaces are baked separately).
        if (!agent.isOnNavMesh)
        {
            var snapFilter = new NavMeshQueryFilter
            {
                areaMask    = NavMesh.AllAreas,
                agentTypeID = agent.agentTypeID
            };
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, snapFilter))
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
        // Note: area costs take effect on the next SetDestination call — no need to reset
        // the current path here, which would cause visible path flicker every recovery tick.
    }

    private void Update()
    {
        // ── Indicator: show '?' when stuck/unreachable; fade out when path is found ──
        if (_indicator != null)
        {
            bool noWaypoints  = waypoints == null || waypoints.Length == 0;
            bool pathComplete = !agent.pathPending && agent.hasPath
                                && agent.pathStatus == NavMeshPathStatus.PathComplete;
            bool pathBlocked  = !agent.pathPending && agent.hasPath
                                && agent.pathStatus != NavMeshPathStatus.PathComplete;
            // "Stranded" = initialized but no path and not waiting for one to compute.
            bool stranded     = initialized && !agent.pathPending && !agent.hasPath;

            bool needsIndicator = noWaypoints || pathBlocked || stranded;

            if (needsIndicator)
            {
                // Debounce: only show after the agent has been stuck for a moment
                // so the '?' doesn't flash during normal between-waypoint gaps.
                _noPathTimer += Time.deltaTime;
                if (_noPathTimer >= (noWaypoints ? 0f : NoPathShowDelay))
                    _indicator.Show();
            }
            else
            {
                _noPathTimer = 0f;
                // FadeOut() is a no-op when nothing is visible or already fading,
                // so safe to call every frame — this covers the case where the '?'
                // was shown before _noPathTimer ever incremented (e.g. via pendingShow).
                if (pathComplete)
                    _indicator.FadeOut();
            }
        }

        // ── Stair / off-mesh link traversal ─────────────────────────────────────
        if (!_traversingLink && agent != null && agent.isOnOffMeshLink)
        {
            StartCoroutine(TraverseLink());
            return;
        }

        if (_traversingLink) return;

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
                    {
                        initialized = true;
                        _indicator?.Hide();
                    }
                }
            }
            return;
        }

        // ── Recovery: re-snap if a runtime bake knocked us off the mesh ─────────
        if (!agent.isOnNavMesh)
        {
            if (Time.frameCount % 30 == 0)
            {
                var recoveryFilter = new NavMeshQueryFilter
                {
                    areaMask    = NavMesh.AllAreas,
                    agentTypeID = agent.agentTypeID
                };
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, recoveryFilter))
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
        {
            bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;
            if (arrived && !hasHonkedThisArrival)
            {
                hasHonkedThisArrival = true;
                if (throttleAudio != null) throttleAudio.TriggerArrivalHonk();
                AudioManager.Mumble(transform.position);
            }
            else if (!arrived && agent.remainingDistance > agent.stoppingDistance + 0.5f)
            {
                hasHonkedThisArrival = false;
            }
        }

        // ── Footsteps ─────────────────────────────────────────────────────────────
        if (_footstepSource != null)
        {
            bool moving = agent.velocity.sqrMagnitude > 0.01f;
            if (moving && !_footstepSource.isPlaying)
                _footstepSource.Play();
            else if (!moving && _footstepSource.isPlaying)
                _footstepSource.Stop();
        }

        // ── Waypoint progression (for agents without AgentAnimation) ─────────────
        if (_agentAnimation == null)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
                GoToRandomWaypoint();
        }
    }

    // Local-space positions of the bottom and top of the stair walkway on the stairwell prefab
    private static readonly Vector3 StairLocalBottom = new Vector3(0.63f, 0f,    0f);
    private static readonly Vector3 StairLocalTop    = new Vector3(0.63f, 1.06f, 1.34f);

    private IEnumerator TraverseLink()
    {
        _traversingLink = true;
        agent.updatePosition = false;
        agent.updateRotation = false;

        // Find the stairwell this agent is crossing
        BuildingData stair = FindNearestStair();

        Vector3 worldBottom, worldTop;
        if (stair != null)
        {
            worldBottom = stair.transform.TransformPoint(StairLocalBottom);
            worldTop    = stair.transform.TransformPoint(StairLocalTop);
        }
        else
        {
            // Fallback: use the raw link endpoints
            OffMeshLinkData fallback = agent.currentOffMeshLinkData;
            worldBottom = fallback.startPos;
            worldTop    = fallback.endPos;
        }

        // Determine direction: whichever end is closer to the agent is the FROM end
        bool goingUp = Vector3.Distance(agent.transform.position, worldBottom)
                     < Vector3.Distance(agent.transform.position, worldTop);
        Vector3 from = goingUp ? worldBottom : worldTop;
        Vector3 to   = goingUp ? worldTop    : worldBottom;

        // Rotate to face horizontal direction of travel
        Vector3 dir = to - from;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            agent.transform.rotation = Quaternion.LookRotation(dir.normalized);

        float dist     = Vector3.Distance(from, to);
        float duration = dist / Mathf.Max(agent.speed, 0.1f);
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            agent.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        agent.transform.position = to;
        agent.CompleteOffMeshLink();
        agent.updatePosition = true;
        agent.updateRotation = true;
        _traversingLink = false;
    }

    // Per-instance cache — static would survive Play Mode restarts with stale destroyed refs
    private BuildingData[] _cachedStairs;

    private BuildingData FindNearestStair()
    {
        if (_cachedStairs == null)
        {
            var all = FindObjectsByType<BuildingData>(FindObjectsSortMode.None);
            var stairs = new System.Collections.Generic.List<BuildingData>();
            foreach (var bd in all)
                if (bd.Data != null && bd.Data.CanUseStairs) stairs.Add(bd);
            _cachedStairs = stairs.ToArray();
        }

        BuildingData nearest = null;
        float nearestDist = 8f;
        foreach (var bd in _cachedStairs)
        {
            if (bd == null) continue;
            float d = Vector3.Distance(agent.transform.position, bd.transform.position);
            if (d < nearestDist) { nearestDist = d; nearest = bd; }
        }
        return nearest;
    }

    public void GoToRandomWaypoint()
    {
        // Always re-scan so newly placed waypoints are picked up immediately.
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
